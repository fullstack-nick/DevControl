using System.Text.Json;
using DevControl.Api.GitHub;
using DevControl.Api.Security;
using DevControl.Application.Apps;
using DevControl.Application.GitHub;
using DevControl.Domain.Entities;
using DevControl.Domain.Enums;
using DevControl.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;

namespace DevControl.Api.Endpoints;

public static class GitHubEndpoints
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static void MapGitHubEndpoints(this WebApplication app)
    {
        var api = app.MapGroup("/api").RequireAuthorization();

        api.MapGet("/organizations/{organizationId:guid}/github/repositories", ResolveRepositoryAsync);
        api.MapGet("/organizations/{organizationId:guid}/github/repo-connections", ListRepoConnectionsAsync);
        api.MapGet("/organizations/{organizationId:guid}/github/onboarding-prs", ListOnboardingPullRequestsAsync);
        api.MapPost("/organizations/{organizationId:guid}/github/onboarding-prs", CreateOnboardingPullRequestAsync).RequireCsrf();
        api.MapPost("/organizations/{organizationId:guid}/github/onboarding-prs/{pullRequestId:guid}/sync", SyncOnboardingPullRequestAsync).RequireCsrf();
        api.MapGet("/organizations/{organizationId:guid}/github/workflow-dispatches", ListWorkflowDispatchesAsync);

        api.MapPost("/organizations/{organizationId:guid}/apps/{liveAppId:guid}/actions/deploy", DispatchDeployAsync).RequireCsrf();
        api.MapPost("/organizations/{organizationId:guid}/apps/{liveAppId:guid}/actions/redeploy", DispatchRedeployAsync).RequireCsrf();
        api.MapPost("/organizations/{organizationId:guid}/apps/{liveAppId:guid}/actions/rollback", DispatchRollbackAsync).RequireCsrf();
    }

    private static async Task<IResult> ResolveRepositoryAsync(
        Guid organizationId,
        string repo,
        CurrentUserAccessor currentUserAccessor,
        TenantAccessService tenantAccess,
        DevControlDbContext dbContext,
        IGitHubAppClient gitHubAppClient,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var actor = await currentUserAccessor.GetOrCreateAsync(cancellationToken);
        var failure = await RequireRoleAsync(organizationId, actor, OrganizationRole.Admin, tenantAccess, "github.repository.resolve.denied", "github_repository", repo, cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        if (!gitHubAppClient.IsConfigured)
        {
            return Results.Problem("GitHub App is not configured.", statusCode: StatusCodes.Status503ServiceUnavailable);
        }

        if (!GitHubRepoNameParser.TryParse(repo, out var parsedRepo))
        {
            return Results.BadRequest(new ProblemDetailsResponse("Repo must use owner/name or a GitHub repository URL."));
        }

        var installation = await UpsertInstallationAsync(organizationId, parsedRepo, gitHubAppClient, dbContext, timeProvider.GetUtcNow(), cancellationToken);
        if (installation is null)
        {
            return Results.NotFound(new ProblemDetailsResponse("GitHub App is not installed on this repository."));
        }

        var repository = await gitHubAppClient.GetRepositoryAsync(parsedRepo, installation.InstallationId, cancellationToken);
        var workflows = await gitHubAppClient.ListWorkflowsAsync(parsedRepo, installation.InstallationId, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Results.Ok(new GitHubRepositoryResolutionResponse(
            repository.FullName,
            repository.DefaultBranch,
            repository.HtmlUrl,
            installation.InstallationId,
            installation.AccountLogin,
            workflows.Select(ToWorkflowResponse).OrderBy(workflow => workflow.Path).ToArray()));
    }

    private static async Task<IResult> ListRepoConnectionsAsync(
        Guid organizationId,
        CurrentUserAccessor currentUserAccessor,
        TenantAccessService tenantAccess,
        DevControlDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var actor = await currentUserAccessor.GetOrCreateAsync(cancellationToken);
        var access = await tenantAccess.RequireAsync(organizationId, actor, OrganizationRole.Viewer, cancellationToken);
        var failure = AccessFailure(access);
        if (failure is not null)
        {
            return failure;
        }

        var connections = await dbContext.GitHubRepoConnections
            .Where(connection => connection.OrganizationId == organizationId)
            .Join(dbContext.Projects, connection => connection.ProjectId, project => project.Id, (connection, project) => new { connection, project })
            .Join(dbContext.ProjectEnvironments, candidate => candidate.connection.EnvironmentId, environment => environment.Id, (candidate, environment) => new { candidate.connection, candidate.project, environment })
            .OrderBy(candidate => candidate.project.Name)
            .ThenBy(candidate => candidate.environment.Name)
            .ThenBy(candidate => candidate.connection.Repo)
            .ToListAsync(cancellationToken);
        return Results.Ok(connections.Select(candidate => ToRepoConnectionResponse(candidate.connection, candidate.project, candidate.environment)));
    }

    private static async Task<IResult> ListOnboardingPullRequestsAsync(
        Guid organizationId,
        CurrentUserAccessor currentUserAccessor,
        TenantAccessService tenantAccess,
        DevControlDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var actor = await currentUserAccessor.GetOrCreateAsync(cancellationToken);
        var access = await tenantAccess.RequireAsync(organizationId, actor, OrganizationRole.Viewer, cancellationToken);
        var failure = AccessFailure(access);
        if (failure is not null)
        {
            return failure;
        }

        var pullRequests = await QueryOnboardingPullRequests(dbContext, organizationId)
            .Take(50)
            .ToListAsync(cancellationToken);
        return Results.Ok(pullRequests);
    }

    private static async Task<IResult> CreateOnboardingPullRequestAsync(
        Guid organizationId,
        GitHubOnboardingCreateRequest request,
        HttpContext httpContext,
        CurrentUserAccessor currentUserAccessor,
        TenantAccessService tenantAccess,
        DevControlDbContext dbContext,
        IGitHubAppClient gitHubAppClient,
        AuditLogWriter auditLogWriter,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var actor = await currentUserAccessor.GetOrCreateAsync(cancellationToken);
        var failure = await RequireRoleAsync(organizationId, actor, OrganizationRole.Admin, tenantAccess, "github.onboarding_pr.create.denied", "github_onboarding_pr", null, cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        if (!gitHubAppClient.IsConfigured)
        {
            return Results.Problem("GitHub App is not configured.", statusCode: StatusCodes.Status503ServiceUnavailable);
        }

        var scope = await LoadScopedEnvironmentAsync(dbContext, organizationId, request.ProjectId, request.EnvironmentId, cancellationToken);
        if (scope is null)
        {
            return Results.NotFound();
        }

        var normalized = NormalizeOnboardingRequest(request);
        if (normalized.Failure is not null)
        {
            return normalized.Failure;
        }

        var input = normalized.Input!;
        var now = timeProvider.GetUtcNow();
        var installation = await UpsertInstallationAsync(organizationId, input.Repo, gitHubAppClient, dbContext, now, cancellationToken);
        if (installation is null)
        {
            return Results.NotFound(new ProblemDetailsResponse("GitHub App is not installed on this repository."));
        }

        var repository = await gitHubAppClient.GetRepositoryAsync(input.Repo, installation.InstallationId, cancellationToken);
        var workflows = await gitHubAppClient.ListWorkflowsAsync(input.Repo, installation.InstallationId, cancellationToken);
        var workflow = workflows.SingleOrDefault(candidate => string.Equals(candidate.Path, input.WorkflowPath, StringComparison.OrdinalIgnoreCase));
        if (workflow is null)
        {
            return Results.BadRequest(new ProblemDetailsResponse("Selected workflow was not found in the repository."));
        }

        var file = await gitHubAppClient.GetFileContentAsync(input.Repo, installation.InstallationId, workflow.Path, repository.DefaultBranch, cancellationToken);
        var serverUrl = AppRegistryEndpoints.BuildPublicBaseUrl(httpContext);
        var audience = AppRegistryEndpoints.BuildRegistrationAudience(httpContext);
        var patch = GitHubWorkflowOnboardingPatchBuilder.Build(new GitHubWorkflowOnboardingRequest(
            file.Content,
            input.JobId,
            serverUrl,
            audience,
            scope.Environment.Slug,
            input.ServiceUrlExpression,
            input.HealthUrlExpression,
            input.VersionExpression,
            input.ImageDigestExpression,
            string.Join(",", input.Capabilities)));
        if (!patch.Succeeded)
        {
            return Results.BadRequest(new GitHubOnboardingValidationResponse(
                [patch.Error ?? "Workflow could not be patched safely."],
                BuildManualOidcSnippet(serverUrl, audience, scope.Environment.Slug, input)));
        }

        var connection = await dbContext.GitHubRepoConnections
            .SingleOrDefaultAsync(candidate =>
                    candidate.OrganizationId == organizationId &&
                    candidate.ProjectId == scope.Project.Id &&
                    candidate.EnvironmentId == scope.Environment.Id &&
                    candidate.NormalizedRepo == input.Repo.NormalizedFullName,
                cancellationToken);
        if (connection is null)
        {
            connection = new GitHubRepoConnection(
                organizationId,
                scope.Project.Id,
                scope.Environment.Id,
                installation.Id,
                repository.FullName,
                input.Repo.NormalizedFullName,
                repository.DefaultBranch,
                workflow.Path,
                workflow.Name,
                input.JobId,
                input.ServiceUrlExpression,
                input.HealthUrlExpression,
                input.VersionExpression,
                input.ImageDigestExpression,
                input.CapabilitiesJson,
                actor.Id,
                now);
            dbContext.GitHubRepoConnections.Add(connection);
        }
        else
        {
            connection.LinkInstallation(installation.Id, now);
            connection.Update(
                repository.FullName,
                input.Repo.NormalizedFullName,
                repository.DefaultBranch,
                workflow.Path,
                workflow.Name,
                input.JobId,
                input.ServiceUrlExpression,
                input.HealthUrlExpression,
                input.VersionExpression,
                input.ImageDigestExpression,
                input.CapabilitiesJson,
                now);
        }

        var environmentBranch = scope.Environment.Slug.Length > 12 ? scope.Environment.Slug[..12] : scope.Environment.Slug;
        var headBranch = $"devcontrol/onboard-{environmentBranch}-{Guid.NewGuid():N}"[..40];
        var pullRequest = await gitHubAppClient.CreateOnboardingPullRequestAsync(
            input.Repo,
            installation.InstallationId,
            repository.DefaultBranch,
            headBranch,
            workflow.Path,
            file.Sha,
            patch.Content,
            $"Add DevControl registration for {scope.Environment.Name}",
            BuildPullRequestBody(serverUrl, scope.Project, scope.Environment, input),
            cancellationToken);

        var onboardingPullRequest = new GitHubOnboardingPullRequest(
            organizationId,
            scope.Project.Id,
            scope.Environment.Id,
            connection.Id,
            repository.FullName,
            workflow.Path,
            repository.DefaultBranch,
            headBranch,
            pullRequest.Number,
            pullRequest.Url,
            actor.Id,
            now);
        dbContext.GitHubOnboardingPullRequests.Add(onboardingPullRequest);

        AddCompletedControlAction(dbContext, organizationId, scope.Project.Id, scope.Environment.Id, actor, "github.onboarding_pr.create", "github_onboarding_pull_request", onboardingPullRequest.Id.ToString(), request, new { pullRequest.Number, pullRequest.Url }, now);
        auditLogWriter.Add(
            organizationId,
            actor,
            "github.onboarding_pr.create",
            "Succeeded",
            "github_onboarding_pull_request",
            onboardingPullRequest.Id.ToString(),
            "GitHub onboarding pull request opened.",
            new { repository.FullName, workflow.Path, pullRequest.Number, pullRequest.Url },
            scope.Project.Id,
            scope.Environment.Id);

        await dbContext.SaveChangesAsync(cancellationToken);
        var response = await QueryOnboardingPullRequests(dbContext, organizationId, onboardingPullRequest.Id)
            .SingleAsync(cancellationToken);
        return Results.Created($"/api/organizations/{organizationId}/github/onboarding-prs/{onboardingPullRequest.Id}", response);
    }

    private static async Task<IResult> SyncOnboardingPullRequestAsync(
        Guid organizationId,
        Guid pullRequestId,
        CurrentUserAccessor currentUserAccessor,
        TenantAccessService tenantAccess,
        DevControlDbContext dbContext,
        IGitHubAppClient gitHubAppClient,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var actor = await currentUserAccessor.GetOrCreateAsync(cancellationToken);
        var failure = await RequireRoleAsync(organizationId, actor, OrganizationRole.Admin, tenantAccess, "github.onboarding_pr.sync.denied", "github_onboarding_pull_request", pullRequestId.ToString(), cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        if (!gitHubAppClient.IsConfigured)
        {
            return Results.Problem("GitHub App is not configured.", statusCode: StatusCodes.Status503ServiceUnavailable);
        }

        var pullRequest = await dbContext.GitHubOnboardingPullRequests
            .SingleOrDefaultAsync(candidate => candidate.OrganizationId == organizationId && candidate.Id == pullRequestId, cancellationToken);
        if (pullRequest is null)
        {
            return Results.NotFound();
        }

        var connection = await dbContext.GitHubRepoConnections.SingleAsync(candidate => candidate.Id == pullRequest.RepoConnectionId, cancellationToken);
        var installation = await dbContext.GitHubInstallations.SingleAsync(candidate => candidate.Id == connection.GitHubInstallationId, cancellationToken);
        if (!GitHubRepoNameParser.TryParse(pullRequest.Repo, out var repo))
        {
            return Results.BadRequest(new ProblemDetailsResponse("Stored pull request repo is invalid."));
        }

        var state = await gitHubAppClient.GetPullRequestAsync(repo, installation.InstallationId, pullRequest.PullRequestNumber, cancellationToken);
        var now = timeProvider.GetUtcNow();
        if (state is null)
        {
            pullRequest.MarkSynced("Missing", null, null, "Pull request was not found in GitHub.", now);
        }
        else
        {
            var status = state.Merged
                ? "Merged"
                : state.State.Equals("closed", StringComparison.OrdinalIgnoreCase)
                    ? "Closed"
                    : "Open";
            pullRequest.MarkSynced(status, state.MergedAt, state.ClosedAt, string.Empty, now);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        var response = await QueryOnboardingPullRequests(dbContext, organizationId, pullRequest.Id)
            .SingleAsync(cancellationToken);
        return Results.Ok(response);
    }

    private static async Task<IResult> ListWorkflowDispatchesAsync(
        Guid organizationId,
        CurrentUserAccessor currentUserAccessor,
        TenantAccessService tenantAccess,
        DevControlDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var actor = await currentUserAccessor.GetOrCreateAsync(cancellationToken);
        var access = await tenantAccess.RequireAsync(organizationId, actor, OrganizationRole.Viewer, cancellationToken);
        var failure = AccessFailure(access);
        if (failure is not null)
        {
            return failure;
        }

        var dispatches = await dbContext.GitHubWorkflowDispatches
            .Where(dispatch => dispatch.OrganizationId == organizationId)
            .Join(dbContext.LiveApps, dispatch => dispatch.LiveAppId, liveApp => liveApp.Id, (dispatch, liveApp) => new { dispatch, liveApp })
            .Join(dbContext.ControlActions, candidate => candidate.dispatch.ControlActionId, controlAction => controlAction.Id, (candidate, controlAction) => new { candidate.dispatch, candidate.liveApp, controlAction })
            .OrderByDescending(candidate => candidate.dispatch.RequestedAt)
            .Take(50)
            .ToListAsync(cancellationToken);
        return Results.Ok(dispatches.Select(candidate => ToWorkflowDispatchResponse(candidate.dispatch, candidate.liveApp, candidate.controlAction)));
    }

    private static Task<IResult> DispatchDeployAsync(
        Guid organizationId,
        Guid liveAppId,
        GitHubAppActionRequest request,
        CurrentUserAccessor currentUserAccessor,
        TenantAccessService tenantAccess,
        DevControlDbContext dbContext,
        IGitHubAppClient gitHubAppClient,
        AuditLogWriter auditLogWriter,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        return DispatchLiveControlAsync("deploy", organizationId, liveAppId, request, currentUserAccessor, tenantAccess, dbContext, gitHubAppClient, auditLogWriter, timeProvider, cancellationToken);
    }

    private static Task<IResult> DispatchRedeployAsync(
        Guid organizationId,
        Guid liveAppId,
        GitHubAppActionRequest request,
        CurrentUserAccessor currentUserAccessor,
        TenantAccessService tenantAccess,
        DevControlDbContext dbContext,
        IGitHubAppClient gitHubAppClient,
        AuditLogWriter auditLogWriter,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        return DispatchLiveControlAsync("redeploy", organizationId, liveAppId, request, currentUserAccessor, tenantAccess, dbContext, gitHubAppClient, auditLogWriter, timeProvider, cancellationToken);
    }

    private static Task<IResult> DispatchRollbackAsync(
        Guid organizationId,
        Guid liveAppId,
        GitHubAppActionRequest request,
        CurrentUserAccessor currentUserAccessor,
        TenantAccessService tenantAccess,
        DevControlDbContext dbContext,
        IGitHubAppClient gitHubAppClient,
        AuditLogWriter auditLogWriter,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        return DispatchLiveControlAsync("rollback", organizationId, liveAppId, request, currentUserAccessor, tenantAccess, dbContext, gitHubAppClient, auditLogWriter, timeProvider, cancellationToken);
    }

    private static async Task<IResult> DispatchLiveControlAsync(
        string action,
        Guid organizationId,
        Guid liveAppId,
        GitHubAppActionRequest request,
        CurrentUserAccessor currentUserAccessor,
        TenantAccessService tenantAccess,
        DevControlDbContext dbContext,
        IGitHubAppClient gitHubAppClient,
        AuditLogWriter auditLogWriter,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var actor = await currentUserAccessor.GetOrCreateAsync(cancellationToken);
        var failure = await RequireRoleAsync(organizationId, actor, OrganizationRole.Admin, tenantAccess, $"github.workflow_dispatch.{action}.denied", "live_app", liveAppId.ToString(), cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        if (!gitHubAppClient.IsConfigured)
        {
            return Results.Problem("GitHub App is not configured.", statusCode: StatusCodes.Status503ServiceUnavailable);
        }

        var reason = request.Reason?.Trim() ?? string.Empty;
        if (reason.Length == 0)
        {
            return Results.BadRequest(new ProblemDetailsResponse("Reason is required."));
        }

        var liveApp = await dbContext.LiveApps
            .SingleOrDefaultAsync(candidate => candidate.OrganizationId == organizationId && candidate.Id == liveAppId, cancellationToken);
        if (liveApp is null)
        {
            return Results.NotFound();
        }

        if (!HasCapability(liveApp.CapabilitiesJson, action))
        {
            return Results.BadRequest(new ProblemDetailsResponse($"Live app does not declare the '{action}' capability."));
        }

        var connection = await dbContext.GitHubRepoConnections
            .SingleOrDefaultAsync(
                candidate =>
                    candidate.OrganizationId == organizationId &&
                    (candidate.LiveAppId == liveApp.Id ||
                     (candidate.ProjectId == liveApp.ProjectId &&
                      candidate.EnvironmentId == liveApp.EnvironmentId &&
                      candidate.NormalizedRepo == liveApp.NormalizedRepo)),
                cancellationToken);
        if (connection is null)
        {
            return Results.BadRequest(new ProblemDetailsResponse("Live app is not connected to a GitHub repo workflow."));
        }

        if (!GitHubRepoNameParser.TryParse(connection.Repo, out var repo))
        {
            return Results.BadRequest(new ProblemDetailsResponse("Connected repository is invalid."));
        }

        var installation = await dbContext.GitHubInstallations.SingleAsync(candidate => candidate.Id == connection.GitHubInstallationId, cancellationToken);
        var rollbackTarget = await LoadRollbackTargetAsync(action, request.TargetDeploymentId, liveApp, dbContext, cancellationToken);
        if (rollbackTarget.Failure is not null)
        {
            return rollbackTarget.Failure;
        }

        var now = timeProvider.GetUtcNow();
        var controlAction = new ControlAction(
            organizationId,
            liveApp.ProjectId,
            liveApp.EnvironmentId,
            $"github.workflow_dispatch.{action}",
            actor.Id,
            "live_app",
            liveApp.Id.ToString(),
            JsonSerializer.Serialize(new { action, reason, request.TargetDeploymentId }, JsonOptions),
            now);
        controlAction.MarkStarted(now);
        dbContext.ControlActions.Add(controlAction);

        var inputs = BuildDispatchInputs(action, controlAction.Id, liveApp, reason, rollbackTarget.Deployment);
        GitHubWorkflowDispatchInfo dispatchInfo;
        try
        {
            dispatchInfo = await gitHubAppClient.DispatchWorkflowAsync(repo, installation.InstallationId, connection.WorkflowPath, connection.DefaultBranch, inputs, now, cancellationToken);
        }
        catch (Exception exception) when (exception is HttpRequestException or InvalidOperationException)
        {
            controlAction.MarkCompleted(
                ControlActionStatus.Failed,
                JsonSerializer.Serialize(new { error = exception.Message }, JsonOptions),
                null,
                now);
            auditLogWriter.Add(
                organizationId,
                actor,
                $"github.workflow_dispatch.{action}",
                "Failed",
                "live_app",
                liveApp.Id.ToString(),
                "GitHub workflow dispatch failed.",
                new { action, connection.Repo, connection.WorkflowPath, error = exception.Message },
                liveApp.ProjectId,
                liveApp.EnvironmentId);
            await dbContext.SaveChangesAsync(cancellationToken);
            return Results.Problem($"GitHub workflow dispatch failed: {exception.Message}", statusCode: StatusCodes.Status502BadGateway);
        }

        var runUrl = !string.IsNullOrWhiteSpace(dispatchInfo.RunUrl)
            ? dispatchInfo.RunUrl
            : dispatchInfo.RunId.HasValue
                ? $"https://github.com/{repo.FullName}/actions/runs/{dispatchInfo.RunId.Value}"
                : string.Empty;
        var dispatch = new GitHubWorkflowDispatch(
            organizationId,
            liveApp.ProjectId,
            liveApp.EnvironmentId,
            connection.Id,
            liveApp.Id,
            controlAction.Id,
            action,
            connection.Repo,
            connection.WorkflowPath,
            connection.DefaultBranch,
            dispatchInfo.RunId,
            runUrl,
            JsonSerializer.Serialize(inputs, JsonOptions),
            actor.Id,
            now);
        dbContext.GitHubWorkflowDispatches.Add(dispatch);
        auditLogWriter.Add(
            organizationId,
            actor,
            $"github.workflow_dispatch.{action}",
            "Succeeded",
            "live_app",
            liveApp.Id.ToString(),
            "GitHub workflow dispatch requested.",
            new { action, connection.Repo, connection.WorkflowPath, dispatchInfo.RunId, runUrl },
            liveApp.ProjectId,
            liveApp.EnvironmentId);

        await dbContext.SaveChangesAsync(cancellationToken);
        return Results.Accepted($"/api/organizations/{organizationId}/github/workflow-dispatches/{dispatch.Id}", ToWorkflowDispatchResponse(dispatch, liveApp, controlAction));
    }

    private static async Task<GitHubInstallation?> UpsertInstallationAsync(
        Guid organizationId,
        GitHubRepoName repo,
        IGitHubAppClient gitHubAppClient,
        DevControlDbContext dbContext,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var installationInfo = await gitHubAppClient.GetRepositoryInstallationAsync(repo, cancellationToken);
        if (installationInfo is null)
        {
            return null;
        }

        var installation = await dbContext.GitHubInstallations
            .SingleOrDefaultAsync(
                candidate =>
                    candidate.OrganizationId == organizationId &&
                    candidate.InstallationId == installationInfo.InstallationId,
                cancellationToken);
        if (installation is null)
        {
            installation = new GitHubInstallation(
                organizationId,
                installationInfo.InstallationId,
                installationInfo.AccountLogin,
                installationInfo.AccountType,
                installationInfo.RepositorySelection,
                installationInfo.PermissionsJson,
                now);
            dbContext.GitHubInstallations.Add(installation);
        }
        else
        {
            installation.Update(
                installationInfo.AccountLogin,
                installationInfo.AccountType,
                installationInfo.RepositorySelection,
                installationInfo.PermissionsJson,
                now);
        }

        return installation;
    }

    private static NormalizedOnboardingResult NormalizeOnboardingRequest(GitHubOnboardingCreateRequest request)
    {
        if (!GitHubRepoNameParser.TryParse(request.Repo, out var repo))
        {
            return NormalizedOnboardingResult.Failed(Results.BadRequest(new ProblemDetailsResponse("Repo must use owner/name or a GitHub repository URL.")));
        }

        var workflowPath = request.WorkflowPath?.Trim() ?? string.Empty;
        if (!workflowPath.StartsWith(".github/workflows/", StringComparison.Ordinal) || !workflowPath.EndsWith(".yml", StringComparison.OrdinalIgnoreCase) && !workflowPath.EndsWith(".yaml", StringComparison.OrdinalIgnoreCase))
        {
            return NormalizedOnboardingResult.Failed(Results.BadRequest(new ProblemDetailsResponse("Workflow path must point to a .github/workflows YAML file.")));
        }

        var jobId = request.JobId?.Trim() ?? string.Empty;
        var serviceUrlExpression = NormalizeExpression(request.ServiceUrlExpression, "Service URL expression");
        var healthUrlExpression = NormalizeExpression(request.HealthUrlExpression, "Health URL expression");
        var versionExpression = NormalizeExpression(request.VersionExpression, "Version expression");
        var imageDigestExpression = NormalizeExpression(request.ImageDigestExpression, "Image digest expression");
        var expressionErrors = new[]
            {
                serviceUrlExpression.Error,
                healthUrlExpression.Error,
                versionExpression.Error,
                imageDigestExpression.Error
            }
            .Where(error => error is not null)
            .Select(error => error!)
            .ToArray();
        if (expressionErrors.Length > 0)
        {
            return NormalizedOnboardingResult.Failed(Results.BadRequest(new ValidationProblemDetailsResponse(expressionErrors)));
        }

        var capabilities = NormalizeCapabilities(request.Capabilities, out var capabilitiesJson, out var capabilityErrors);
        if (capabilityErrors.Count > 0)
        {
            return NormalizedOnboardingResult.Failed(Results.BadRequest(new ValidationProblemDetailsResponse(capabilityErrors)));
        }

        return NormalizedOnboardingResult.Success(new NormalizedOnboardingInput(
            repo,
            workflowPath,
            jobId,
            serviceUrlExpression.Value,
            healthUrlExpression.Value,
            versionExpression.Value,
            imageDigestExpression.Value,
            capabilities,
            capabilitiesJson));
    }

    private static NormalizedExpression NormalizeExpression(string? raw, string label)
    {
        var value = raw?.Trim() ?? string.Empty;
        if (value.Length == 0)
        {
            return new NormalizedExpression(string.Empty, $"{label} is required.");
        }

        if (value.Contains('\n', StringComparison.Ordinal) || value.Contains('\r', StringComparison.Ordinal))
        {
            return new NormalizedExpression(string.Empty, $"{label} cannot contain line breaks.");
        }

        return value.Length > 500
            ? new NormalizedExpression(string.Empty, $"{label} is too long.")
            : new NormalizedExpression(value, null);
    }

    private static IReadOnlyList<string> NormalizeCapabilities(IReadOnlyList<string>? rawCapabilities, out string capabilitiesJson, out IReadOnlyList<string> errors)
    {
        var values = rawCapabilities is { Count: > 0 }
            ? rawCapabilities
            : ["health", "deployment-events"];
        var normalized = new SortedSet<string>(StringComparer.Ordinal);
        var validationErrors = new List<string>();
        foreach (var rawCapability in values)
        {
            var capability = rawCapability.Trim().ToLowerInvariant();
            if (capability.Length == 0)
            {
                continue;
            }

            if (!AppRegistrationValidator.KnownCapabilities.Contains(capability))
            {
                validationErrors.Add($"Unsupported capability '{rawCapability}'.");
            }
            else
            {
                normalized.Add(capability);
            }
        }

        if (normalized.Count == 0)
        {
            validationErrors.Add("At least one capability is required.");
        }

        capabilitiesJson = JsonSerializer.Serialize(normalized.ToArray(), JsonOptions);
        errors = validationErrors;
        return normalized.ToArray();
    }

    private static IReadOnlyDictionary<string, string> BuildDispatchInputs(
        string action,
        Guid controlActionId,
        LiveApp liveApp,
        string reason,
        LiveAppDeployment? rollbackTarget)
    {
        var inputs = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["devcontrol_action"] = action,
            ["devcontrol_action_id"] = controlActionId.ToString(),
            ["devcontrol_environment"] = liveApp.EnvironmentId.ToString(),
            ["devcontrol_reason"] = reason
        };

        if (rollbackTarget is not null)
        {
            inputs["devcontrol_rollback_deployment_id"] = rollbackTarget.Id.ToString();
            inputs["devcontrol_rollback_commit_sha"] = rollbackTarget.CommitSha;
            inputs["devcontrol_rollback_version"] = rollbackTarget.Version;
            inputs["devcontrol_rollback_image_digest"] = rollbackTarget.ImageDigest;
        }

        return inputs;
    }

    private static async Task<RollbackTargetResult> LoadRollbackTargetAsync(
        string action,
        Guid? targetDeploymentId,
        LiveApp liveApp,
        DevControlDbContext dbContext,
        CancellationToken cancellationToken)
    {
        if (!action.Equals("rollback", StringComparison.Ordinal))
        {
            return RollbackTargetResult.Success(null);
        }

        if (targetDeploymentId is null)
        {
            return RollbackTargetResult.Failed(Results.BadRequest(new ProblemDetailsResponse("Rollback requires targetDeploymentId.")));
        }

        var deployment = await dbContext.LiveAppDeployments
            .SingleOrDefaultAsync(
                candidate =>
                    candidate.Id == targetDeploymentId &&
                    candidate.OrganizationId == liveApp.OrganizationId &&
                    candidate.LiveAppId == liveApp.Id,
                cancellationToken);
        return deployment is null
            ? RollbackTargetResult.Failed(Results.BadRequest(new ProblemDetailsResponse("Rollback target deployment was not found for this live app.")))
            : RollbackTargetResult.Success(deployment);
    }

    private static bool HasCapability(string capabilitiesJson, string action)
    {
        try
        {
            return (JsonSerializer.Deserialize<string[]>(capabilitiesJson, JsonOptions) ?? [])
                .Any(capability => capability.Equals(action, StringComparison.OrdinalIgnoreCase));
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static string BuildPullRequestBody(string serverUrl, Project project, ProjectEnvironment environment, NormalizedOnboardingInput input)
    {
        return $"""
               DevControl generated this pull request to register deployments from `{input.WorkflowPath}` after the selected job completes.

               Project: `{project.Slug}`
               Environment: `{environment.Slug}`
               DevControl: {serverUrl}

               The workflow change uses GitHub Actions OIDC with `id-token: write`; no DevControl registration secret is added to this repository.
               """;
    }

    private static string BuildManualOidcSnippet(string serverUrl, string audience, string environmentSlug, NormalizedOnboardingInput input)
    {
        return string.Join('\n', new[]
        {
            "permissions:",
            "  id-token: write",
            "",
            "steps:",
            "  - name: Install DevControl CLI",
            "    uses: fullstack-nick/DevControl/.github/actions/setup-devcontrol@main",
            "",
            "  - name: Request DevControl OIDC token",
            "    uses: actions/github-script@v8",
            "    id: devcontrol_oidc",
            "    with:",
            "      script: |",
            "        const core = require('@actions/core')",
            $"        const token = await core.getIDToken('{audience}')",
            "        core.setSecret(token)",
            "        core.setOutput('token', token)",
            "",
            "  - name: Register app in DevControl",
            "    env:",
            $"      DEVCONTROL_SERVER: {serverUrl}",
            "      DEVCONTROL_GITHUB_OIDC_TOKEN: ${{ steps.devcontrol_oidc.outputs.token }}",
            "    run: |",
            "      devcontrol apps register \\",
            $"        --environment {environmentSlug} \\",
            $"        --service-url \"{input.ServiceUrlExpression}\" \\",
            $"        --health-url \"{input.HealthUrlExpression}\" \\",
            "        --repo \"${{ github.repository }}\" \\",
            "        --commit-sha \"${{ github.sha }}\" \\",
            $"        --version \"{input.VersionExpression}\" \\",
            $"        --image-digest \"{input.ImageDigestExpression}\" \\",
            $"        --capabilities {string.Join(",", input.Capabilities)} \\",
            "        --github-oidc-token \"$DEVCONTROL_GITHUB_OIDC_TOKEN\" \\",
            "        --json"
        });
    }

    private static IQueryable<GitHubRepoConnectionResponse> QueryRepoConnections(DevControlDbContext dbContext, Guid organizationId)
    {
        return dbContext.GitHubRepoConnections
            .Where(connection => connection.OrganizationId == organizationId)
            .Join(dbContext.Projects, connection => connection.ProjectId, project => project.Id, (connection, project) => new { connection, project })
            .Join(dbContext.ProjectEnvironments, candidate => candidate.connection.EnvironmentId, environment => environment.Id, (candidate, environment) => new { candidate.connection, candidate.project, environment })
            .OrderBy(candidate => candidate.project.Name)
            .ThenBy(candidate => candidate.environment.Name)
            .ThenBy(candidate => candidate.connection.Repo)
            .Select(candidate => ToRepoConnectionResponse(candidate.connection, candidate.project, candidate.environment));
    }

    private static IQueryable<GitHubOnboardingPullRequestResponse> QueryOnboardingPullRequests(DevControlDbContext dbContext, Guid organizationId, Guid? pullRequestId = null)
    {
        var pullRequests = dbContext.GitHubOnboardingPullRequests
            .Where(pullRequest => pullRequest.OrganizationId == organizationId);
        if (pullRequestId.HasValue)
        {
            pullRequests = pullRequests.Where(pullRequest => pullRequest.Id == pullRequestId.Value);
        }

        return pullRequests
            .Join(dbContext.Projects, pullRequest => pullRequest.ProjectId, project => project.Id, (pullRequest, project) => new { pullRequest, project })
            .Join(dbContext.ProjectEnvironments, candidate => candidate.pullRequest.EnvironmentId, environment => environment.Id, (candidate, environment) => new { candidate.pullRequest, candidate.project, environment })
            .OrderByDescending(candidate => candidate.pullRequest.CreatedAt)
            .Select(candidate => new GitHubOnboardingPullRequestResponse(
                candidate.pullRequest.Id,
                candidate.pullRequest.RepoConnectionId,
                candidate.pullRequest.ProjectId,
                candidate.project.Name,
                candidate.project.Slug,
                candidate.pullRequest.EnvironmentId,
                candidate.environment.Name,
                candidate.environment.Slug,
                candidate.pullRequest.Repo,
                candidate.pullRequest.WorkflowPath,
                candidate.pullRequest.BaseBranch,
                candidate.pullRequest.HeadBranch,
                candidate.pullRequest.PullRequestNumber,
                candidate.pullRequest.PullRequestUrl,
                candidate.pullRequest.Status,
                candidate.pullRequest.Error,
                candidate.pullRequest.CreatedAt,
                candidate.pullRequest.UpdatedAt,
                candidate.pullRequest.MergedAt,
                candidate.pullRequest.ClosedAt));
    }

    private static IQueryable<GitHubWorkflowDispatchResponse> QueryWorkflowDispatches(DevControlDbContext dbContext, Guid organizationId)
    {
        return dbContext.GitHubWorkflowDispatches
            .Where(dispatch => dispatch.OrganizationId == organizationId)
            .Join(dbContext.LiveApps, dispatch => dispatch.LiveAppId, liveApp => liveApp.Id, (dispatch, liveApp) => new { dispatch, liveApp })
            .Join(dbContext.ControlActions, candidate => candidate.dispatch.ControlActionId, controlAction => controlAction.Id, (candidate, controlAction) => new { candidate.dispatch, candidate.liveApp, controlAction })
            .OrderByDescending(candidate => candidate.dispatch.RequestedAt)
            .Select(candidate => ToWorkflowDispatchResponse(candidate.dispatch, candidate.liveApp, candidate.controlAction));
    }

    private static GitHubWorkflowInfoResponse ToWorkflowResponse(GitHubWorkflowInfo workflow)
    {
        return new GitHubWorkflowInfoResponse(workflow.Id, workflow.Name, workflow.Path, workflow.State);
    }

    private static GitHubRepoConnectionResponse ToRepoConnectionResponse(GitHubRepoConnection connection, Project project, ProjectEnvironment environment)
    {
        return new GitHubRepoConnectionResponse(
            connection.Id,
            connection.LiveAppId,
            connection.ProjectId,
            project.Name,
            project.Slug,
            connection.EnvironmentId,
            environment.Name,
            environment.Slug,
            connection.Repo,
            connection.DefaultBranch,
            connection.WorkflowPath,
            connection.WorkflowName,
            connection.JobId,
            ReadCapabilities(connection.CapabilitiesJson),
            connection.CreatedAt,
            connection.UpdatedAt);
    }

    private static GitHubWorkflowDispatchResponse ToWorkflowDispatchResponse(GitHubWorkflowDispatch dispatch, LiveApp liveApp, ControlAction controlAction)
    {
        return new GitHubWorkflowDispatchResponse(
            dispatch.Id,
            dispatch.ControlActionId,
            controlAction.Status.ToString(),
            dispatch.LiveAppId,
            liveApp.Repo,
            dispatch.Action,
            dispatch.Repo,
            dispatch.WorkflowPath,
            dispatch.Ref,
            dispatch.GitHubRunId,
            dispatch.RunUrl,
            dispatch.Status,
            dispatch.Conclusion,
            dispatch.RequestedAt,
            dispatch.UpdatedAt,
            dispatch.CompletedAt);
    }

    private static IReadOnlyList<string> ReadCapabilities(string capabilitiesJson)
    {
        try
        {
            return JsonSerializer.Deserialize<string[]>(capabilitiesJson, JsonOptions) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static async Task<IResult?> RequireRoleAsync(
        Guid organizationId,
        CurrentUser actor,
        OrganizationRole role,
        TenantAccessService tenantAccess,
        string deniedAction,
        string targetType,
        string? targetId,
        CancellationToken cancellationToken)
    {
        var access = await tenantAccess.RequireAsync(
            organizationId,
            actor,
            role,
            cancellationToken,
            auditDenied: true,
            deniedAction: deniedAction,
            targetType: targetType,
            targetId: targetId);
        return AccessFailure(access);
    }

    private static async Task<ScopedEnvironment?> LoadScopedEnvironmentAsync(
        DevControlDbContext dbContext,
        Guid organizationId,
        Guid projectId,
        Guid environmentId,
        CancellationToken cancellationToken)
    {
        var project = await dbContext.Projects
            .SingleOrDefaultAsync(candidate => candidate.OrganizationId == organizationId && candidate.Id == projectId, cancellationToken);
        var environment = await dbContext.ProjectEnvironments
            .SingleOrDefaultAsync(
                candidate =>
                    candidate.OrganizationId == organizationId &&
                    candidate.ProjectId == projectId &&
                    candidate.Id == environmentId,
                cancellationToken);

        return project is null || environment is null ? null : new ScopedEnvironment(project, environment);
    }

    private static void AddCompletedControlAction(
        DevControlDbContext dbContext,
        Guid organizationId,
        Guid? projectId,
        Guid? environmentId,
        CurrentUser actor,
        string actionType,
        string targetType,
        string? targetId,
        object request,
        object result,
        DateTimeOffset now)
    {
        var controlAction = new ControlAction(
            organizationId,
            projectId,
            environmentId,
            actionType,
            actor.Id,
            targetType,
            targetId,
            JsonSerializer.Serialize(request, JsonOptions),
            now);
        controlAction.MarkStarted(now);
        controlAction.MarkCompleted(ControlActionStatus.Succeeded, JsonSerializer.Serialize(result, JsonOptions), null, now);
        dbContext.ControlActions.Add(controlAction);
    }

    private static IResult? AccessFailure(TenantAccessResult result)
    {
        return result.Status switch
        {
            TenantAccessStatus.Granted => null,
            TenantAccessStatus.Forbidden => Results.Forbid(),
            _ => Results.NotFound()
        };
    }

    private sealed record ScopedEnvironment(Project Project, ProjectEnvironment Environment);

    private sealed record NormalizedExpression(string Value, string? Error);

    private sealed record NormalizedOnboardingInput(
        GitHubRepoName Repo,
        string WorkflowPath,
        string JobId,
        string ServiceUrlExpression,
        string HealthUrlExpression,
        string VersionExpression,
        string ImageDigestExpression,
        IReadOnlyList<string> Capabilities,
        string CapabilitiesJson);

    private sealed record NormalizedOnboardingResult(NormalizedOnboardingInput? Input, IResult? Failure)
    {
        public static NormalizedOnboardingResult Success(NormalizedOnboardingInput input) => new(input, null);

        public static NormalizedOnboardingResult Failed(IResult failure) => new(null, failure);
    }

    private sealed record RollbackTargetResult(LiveAppDeployment? Deployment, IResult? Failure)
    {
        public static RollbackTargetResult Success(LiveAppDeployment? deployment) => new(deployment, null);

        public static RollbackTargetResult Failed(IResult failure) => new(null, failure);
    }
}

public sealed record GitHubRepositoryResolutionResponse(
    string FullName,
    string DefaultBranch,
    string HtmlUrl,
    long InstallationId,
    string InstallationAccount,
    IReadOnlyList<GitHubWorkflowInfoResponse> Workflows);

public sealed record GitHubWorkflowInfoResponse(long Id, string Name, string Path, string State);

public sealed record GitHubOnboardingCreateRequest(
    Guid ProjectId,
    Guid EnvironmentId,
    string? Repo,
    string? WorkflowPath,
    string? JobId,
    string? ServiceUrlExpression,
    string? HealthUrlExpression,
    string? VersionExpression,
    string? ImageDigestExpression,
    IReadOnlyList<string>? Capabilities);

public sealed record GitHubOnboardingValidationResponse(IReadOnlyList<string> Errors, string ManualSnippet);

public sealed record GitHubRepoConnectionResponse(
    Guid Id,
    Guid? LiveAppId,
    Guid ProjectId,
    string ProjectName,
    string ProjectSlug,
    Guid EnvironmentId,
    string EnvironmentName,
    string EnvironmentSlug,
    string Repo,
    string DefaultBranch,
    string WorkflowPath,
    string WorkflowName,
    string JobId,
    IReadOnlyList<string> Capabilities,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record GitHubOnboardingPullRequestResponse(
    Guid Id,
    Guid RepoConnectionId,
    Guid ProjectId,
    string ProjectName,
    string ProjectSlug,
    Guid EnvironmentId,
    string EnvironmentName,
    string EnvironmentSlug,
    string Repo,
    string WorkflowPath,
    string BaseBranch,
    string HeadBranch,
    int PullRequestNumber,
    string PullRequestUrl,
    string Status,
    string Error,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? MergedAt,
    DateTimeOffset? ClosedAt);

public sealed record GitHubAppActionRequest(string? Reason, Guid? TargetDeploymentId);

public sealed record GitHubWorkflowDispatchResponse(
    Guid Id,
    Guid ControlActionId,
    string ControlActionStatus,
    Guid LiveAppId,
    string LiveAppRepo,
    string Action,
    string Repo,
    string WorkflowPath,
    string Ref,
    long? GitHubRunId,
    string RunUrl,
    string Status,
    string Conclusion,
    DateTimeOffset RequestedAt,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? CompletedAt);
