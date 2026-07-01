using System.Text.Json;
using DevControl.Api.Monitoring;
using DevControl.Api.Security;
using DevControl.Api.Webhooks;
using DevControl.Application.Apps;
using DevControl.Application.Security;
using DevControl.Application.Webhooks;
using DevControl.Domain.Entities;
using DevControl.Domain.Enums;
using DevControl.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;

namespace DevControl.Api.Endpoints;

public static class AppRegistryEndpoints
{
    private const string RegisterScope = "apps:register";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static void MapAppRegistryEndpoints(this WebApplication app)
    {
        app.MapPost("/api/apps/register", RegisterAppAsync);

        var api = app.MapGroup("/api").RequireAuthorization();

        api.MapGet("/organizations/{organizationId:guid}/apps", ListAppsAsync);
        api.MapGet("/organizations/{organizationId:guid}/registration-tokens", ListRegistrationTokensAsync);
        api.MapPost(
            "/organizations/{organizationId:guid}/projects/{projectId:guid}/environments/{environmentId:guid}/registration-tokens",
            CreateRegistrationTokenAsync).RequireCsrf();
        api.MapPost(
            "/organizations/{organizationId:guid}/registration-tokens/{tokenId:guid}/revoke",
            RevokeRegistrationTokenAsync).RequireCsrf();
    }

    private static async Task<IResult> RegisterAppAsync(
        AppRegisterRequest request,
        HttpContext httpContext,
        DevControlDbContext dbContext,
        RegistrationTokenService tokenService,
        AuditLogWriter auditLogWriter,
        MonitorProvisioningService monitorProvisioningService,
        WebhookEventPublisher webhookEventPublisher,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var bearerToken = GetBearerToken(httpContext);
        if (bearerToken is null)
        {
            return Results.Unauthorized();
        }

        var tokenHash = tokenService.HashToken(bearerToken);
        var token = await dbContext.RegistrationTokens
            .SingleOrDefaultAsync(candidate => candidate.TokenHash == tokenHash, cancellationToken);

        if (token is null || token.IsRevoked || token.Scope != RegisterScope)
        {
            return Results.Unauthorized();
        }

        var project = await dbContext.Projects
            .SingleOrDefaultAsync(candidate =>
                    candidate.OrganizationId == token.OrganizationId &&
                    candidate.Id == token.ProjectId,
                cancellationToken);
        var environment = await dbContext.ProjectEnvironments
            .SingleOrDefaultAsync(candidate =>
                    candidate.OrganizationId == token.OrganizationId &&
                    candidate.ProjectId == token.ProjectId &&
                    candidate.Id == token.EnvironmentId,
                cancellationToken);

        if (project is null || environment is null)
        {
            return Results.Unauthorized();
        }

        var validation = AppRegistrationValidator.Validate(new AppRegistrationInput(
            request.Repo,
            request.Environment,
            request.ServiceUrl,
            request.HealthUrl,
            request.CommitSha,
            request.Version,
            request.ImageDigest,
            request.Capabilities));

        if (!validation.IsValid)
        {
            return Results.BadRequest(new ValidationProblemDetailsResponse(validation.Errors));
        }

        var details = validation.Details!;
        if (!string.Equals(details.Environment, environment.Slug, StringComparison.OrdinalIgnoreCase))
        {
            return Results.Forbid();
        }

        var now = timeProvider.GetUtcNow();
        var liveApp = await dbContext.LiveApps
            .SingleOrDefaultAsync(candidate =>
                    candidate.OrganizationId == token.OrganizationId &&
                    candidate.ProjectId == token.ProjectId &&
                    candidate.EnvironmentId == token.EnvironmentId &&
                    candidate.NormalizedRepo == details.NormalizedRepo,
                cancellationToken);

        if (liveApp is null)
        {
            liveApp = new LiveApp(
                token.OrganizationId,
                token.ProjectId,
                token.EnvironmentId,
                details.Repo,
                details.NormalizedRepo,
                details.ServiceUrl,
                details.HealthUrl,
                details.CommitSha,
                details.Version,
                details.ImageDigest,
                details.CapabilitiesJson,
                now);
            dbContext.LiveApps.Add(liveApp);
        }
        else
        {
            liveApp.UpdateRegistration(
                details.Repo,
                details.NormalizedRepo,
                details.ServiceUrl,
                details.HealthUrl,
                details.CommitSha,
                details.Version,
                details.ImageDigest,
                details.CapabilitiesJson,
                now);
        }

        dbContext.LiveAppDeployments.Add(new LiveAppDeployment(
            liveApp.Id,
            token.OrganizationId,
            token.ProjectId,
            token.EnvironmentId,
            details.Repo,
            details.ServiceUrl,
            details.HealthUrl,
            details.CommitSha,
            details.Version,
            details.ImageDigest,
            details.CapabilitiesJson,
            now));
        await monitorProvisioningService.EnsureManagedMonitorAsync(liveApp, token.CreatedByUserId, now, cancellationToken);
        token.MarkUsed(now);

        auditLogWriter.Add(
            token.OrganizationId,
            null,
            "app.register",
            "Succeeded",
            "live_app",
            liveApp.Id.ToString(),
            "Live app registration received.",
            new
            {
                details.Repo,
                details.Environment,
                token.TokenPrefix,
                details.CommitSha,
                details.Version,
                capabilities = details.Capabilities
            },
            token.ProjectId,
            token.EnvironmentId);
        await webhookEventPublisher.PublishAsync(
            token.OrganizationId,
            token.ProjectId,
            token.EnvironmentId,
            WebhookEventTypes.AppRegistered,
            "live_app",
            liveApp.Id.ToString(),
            null,
            "system",
            new
            {
                liveApp.Id,
                details.Repo,
                details.Environment,
                details.ServiceUrl,
                details.HealthUrl,
                details.CommitSha,
                details.Version,
                details.ImageDigest,
                capabilities = details.Capabilities
            },
            now,
            cancellationToken);

        await dbContext.SaveChangesAsync(cancellationToken);
        return Results.Ok(ToLiveAppResponse(liveApp, project.Name, project.Slug, environment.Name, environment.Slug, details.Capabilities));
    }

    private static async Task<IResult> ListAppsAsync(
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

        var apps = await dbContext.LiveApps
            .Where(liveApp => liveApp.OrganizationId == organizationId)
            .Join(
                dbContext.Projects,
                liveApp => liveApp.ProjectId,
                project => project.Id,
                (liveApp, project) => new { liveApp, project })
            .Join(
                dbContext.ProjectEnvironments,
                candidate => candidate.liveApp.EnvironmentId,
                environment => environment.Id,
                (candidate, environment) => new { candidate.liveApp, candidate.project, environment })
            .OrderBy(candidate => candidate.project.Name)
            .ThenBy(candidate => candidate.environment.Name)
            .ThenBy(candidate => candidate.liveApp.Repo)
            .ToListAsync(cancellationToken);

        return Results.Ok(apps.Select(candidate => ToLiveAppResponse(
            candidate.liveApp,
            candidate.project.Name,
            candidate.project.Slug,
            candidate.environment.Name,
            candidate.environment.Slug,
            DeserializeCapabilities(candidate.liveApp.CapabilitiesJson))));
    }

    private static async Task<IResult> ListRegistrationTokensAsync(
        Guid organizationId,
        CurrentUserAccessor currentUserAccessor,
        TenantAccessService tenantAccess,
        DevControlDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var actor = await currentUserAccessor.GetOrCreateAsync(cancellationToken);
        var access = await tenantAccess.RequireAsync(organizationId, actor, OrganizationRole.Admin, cancellationToken);
        var failure = AccessFailure(access);
        if (failure is not null)
        {
            return failure;
        }

        var tokens = await dbContext.RegistrationTokens
            .Where(token => token.OrganizationId == organizationId)
            .Join(
                dbContext.Projects,
                token => token.ProjectId,
                project => project.Id,
                (token, project) => new { token, project })
            .Join(
                dbContext.ProjectEnvironments,
                candidate => candidate.token.EnvironmentId,
                environment => environment.Id,
                (candidate, environment) => new { candidate.token, candidate.project, environment })
            .OrderByDescending(candidate => candidate.token.CreatedAt)
            .Select(candidate => new RegistrationTokenResponse(
                candidate.token.Id,
                candidate.token.Name,
                candidate.token.TokenPrefix,
                candidate.token.Scope,
                candidate.project.Id,
                candidate.project.Name,
                candidate.project.Slug,
                candidate.environment.Id,
                candidate.environment.Name,
                candidate.environment.Slug,
                candidate.token.CreatedAt,
                candidate.token.LastUsedAt,
                candidate.token.RevokedAt))
            .ToListAsync(cancellationToken);

        return Results.Ok(tokens);
    }

    private static async Task<IResult> CreateRegistrationTokenAsync(
        Guid organizationId,
        Guid projectId,
        Guid environmentId,
        RegistrationTokenCreateRequest request,
        HttpContext httpContext,
        CurrentUserAccessor currentUserAccessor,
        TenantAccessService tenantAccess,
        DevControlDbContext dbContext,
        AuditLogWriter auditLogWriter,
        RegistrationTokenService tokenService,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var actor = await currentUserAccessor.GetOrCreateAsync(cancellationToken);
        var access = await tenantAccess.RequireAsync(
            organizationId,
            actor,
            OrganizationRole.Admin,
            cancellationToken,
            auditDenied: true,
            deniedAction: "registration_token.create.denied",
            targetType: "registration_token");
        var failure = AccessFailure(access);
        if (failure is not null)
        {
            return failure;
        }

        var project = await dbContext.Projects
            .SingleOrDefaultAsync(candidate => candidate.OrganizationId == organizationId && candidate.Id == projectId, cancellationToken);
        var environment = await dbContext.ProjectEnvironments
            .SingleOrDefaultAsync(candidate =>
                    candidate.OrganizationId == organizationId &&
                    candidate.ProjectId == projectId &&
                    candidate.Id == environmentId,
                cancellationToken);

        if (project is null || environment is null)
        {
            return Results.NotFound();
        }

        var name = string.IsNullOrWhiteSpace(request.Name)
            ? $"{project.Slug}/{environment.Slug} registration"
            : request.Name.Trim();
        var secret = tokenService.CreateToken();
        var now = timeProvider.GetUtcNow();
        var token = new RegistrationToken(
            organizationId,
            projectId,
            environmentId,
            name,
            secret.Prefix,
            secret.Hash,
            RegisterScope,
            actor.Id,
            now);

        dbContext.RegistrationTokens.Add(token);
        auditLogWriter.Add(
            organizationId,
            actor,
            "registration_token.create",
            "Succeeded",
            "registration_token",
            token.Id.ToString(),
            "Registration token created.",
            new { token.Name, token.TokenPrefix, token.Scope, projectSlug = project.Slug, environmentSlug = environment.Slug },
            projectId,
            environmentId);

        await dbContext.SaveChangesAsync(cancellationToken);

        var snippet = WorkflowSnippetBuilder.Build(new WorkflowSnippetContext(
            $"{httpContext.Request.Scheme}://{httpContext.Request.Host}",
            secret.Secret,
            environment.Slug,
            "<service-url-from-deploy-step>",
            "<health-url-from-deploy-step>",
            "${{ github.ref_name }}",
            "<image-digest-from-deploy-step>",
            "health,deployment-events"));

        return Results.Created(
            $"/api/organizations/{organizationId}/registration-tokens/{token.Id}",
            new RegistrationTokenCreateResponse(
                token.Id,
                token.Name,
                token.TokenPrefix,
                token.Scope,
                project.Id,
                project.Name,
                project.Slug,
                environment.Id,
                environment.Name,
                environment.Slug,
                token.CreatedAt,
                secret.Secret,
                snippet));
    }

    private static async Task<IResult> RevokeRegistrationTokenAsync(
        Guid organizationId,
        Guid tokenId,
        CurrentUserAccessor currentUserAccessor,
        TenantAccessService tenantAccess,
        DevControlDbContext dbContext,
        AuditLogWriter auditLogWriter,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var actor = await currentUserAccessor.GetOrCreateAsync(cancellationToken);
        var access = await tenantAccess.RequireAsync(
            organizationId,
            actor,
            OrganizationRole.Admin,
            cancellationToken,
            auditDenied: true,
            deniedAction: "registration_token.revoke.denied",
            targetType: "registration_token",
            targetId: tokenId.ToString());
        var failure = AccessFailure(access);
        if (failure is not null)
        {
            return failure;
        }

        var token = await dbContext.RegistrationTokens
            .SingleOrDefaultAsync(candidate => candidate.OrganizationId == organizationId && candidate.Id == tokenId, cancellationToken);
        if (token is null)
        {
            return Results.NotFound();
        }

        token.Revoke(actor.Id, timeProvider.GetUtcNow());
        auditLogWriter.Add(
            organizationId,
            actor,
            "registration_token.revoke",
            "Succeeded",
            "registration_token",
            token.Id.ToString(),
            "Registration token revoked.",
            new { token.Name, token.TokenPrefix, token.Scope },
            token.ProjectId,
            token.EnvironmentId);

        await dbContext.SaveChangesAsync(cancellationToken);
        return Results.Ok(new RegistrationTokenRevokeResponse(token.Id, token.RevokedAt));
    }

    private static string? GetBearerToken(HttpContext httpContext)
    {
        var authorization = httpContext.Request.Headers.Authorization.ToString();
        if (authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            var token = authorization["Bearer ".Length..].Trim();
            return string.IsNullOrWhiteSpace(token) ? null : token;
        }

        return null;
    }

    private static LiveAppResponse ToLiveAppResponse(
        LiveApp liveApp,
        string projectName,
        string projectSlug,
        string environmentName,
        string environmentSlug,
        IReadOnlyList<string> capabilities)
    {
        return new LiveAppResponse(
            liveApp.Id,
            liveApp.ProjectId,
            projectName,
            projectSlug,
            liveApp.EnvironmentId,
            environmentName,
            environmentSlug,
            liveApp.Repo,
            liveApp.ServiceUrl,
            liveApp.HealthUrl,
            liveApp.CurrentCommitSha,
            liveApp.Version,
            liveApp.ImageDigest,
            capabilities,
            liveApp.CreatedAt,
            liveApp.LastRegisteredAt);
    }

    private static IReadOnlyList<string> DeserializeCapabilities(string capabilitiesJson)
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

    private static IResult? AccessFailure(TenantAccessResult result)
    {
        return result.Status switch
        {
            TenantAccessStatus.Granted => null,
            TenantAccessStatus.Forbidden => Results.Forbid(),
            _ => Results.NotFound()
        };
    }
}

public sealed record AppRegisterRequest(
    string? Repo,
    string? Environment,
    string? ServiceUrl,
    string? HealthUrl,
    string? CommitSha,
    string? Version,
    string? ImageDigest,
    IReadOnlyList<string>? Capabilities);

public sealed record ValidationProblemDetailsResponse(IReadOnlyList<string> Errors);

public sealed record LiveAppResponse(
    Guid Id,
    Guid ProjectId,
    string ProjectName,
    string ProjectSlug,
    Guid EnvironmentId,
    string EnvironmentName,
    string EnvironmentSlug,
    string Repo,
    string ServiceUrl,
    string HealthUrl,
    string CurrentCommitSha,
    string Version,
    string ImageDigest,
    IReadOnlyList<string> Capabilities,
    DateTimeOffset CreatedAt,
    DateTimeOffset LastRegisteredAt);

public sealed record RegistrationTokenCreateRequest(string? Name);

public sealed record RegistrationTokenResponse(
    Guid Id,
    string Name,
    string TokenPrefix,
    string Scope,
    Guid ProjectId,
    string ProjectName,
    string ProjectSlug,
    Guid EnvironmentId,
    string EnvironmentName,
    string EnvironmentSlug,
    DateTimeOffset CreatedAt,
    DateTimeOffset? LastUsedAt,
    DateTimeOffset? RevokedAt);

public sealed record RegistrationTokenCreateResponse(
    Guid Id,
    string Name,
    string TokenPrefix,
    string Scope,
    Guid ProjectId,
    string ProjectName,
    string ProjectSlug,
    Guid EnvironmentId,
    string EnvironmentName,
    string EnvironmentSlug,
    DateTimeOffset CreatedAt,
    string Secret,
    string WorkflowSnippet);

public sealed record RegistrationTokenRevokeResponse(Guid Id, DateTimeOffset? RevokedAt);
