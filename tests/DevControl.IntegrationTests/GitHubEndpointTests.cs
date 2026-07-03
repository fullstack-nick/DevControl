using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using DevControl.Api.GitHub;
using DevControl.Application.Email;
using DevControl.Application.GitHub;
using DevControl.Infrastructure.Database;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace DevControl.IntegrationTests;

[Collection(PostgresIntegrationCollection.Name)]
public sealed partial class GitHubEndpointTests
{
    [Fact]
    public async Task AdminCanOpenOnboardingPr_ViewerCannot()
    {
        var connectionString = Environment.GetEnvironmentVariable("DEVCONTROL_TEST_CONNECTION_STRING");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return;
        }

        var fakeGitHub = new FakeGitHubAppClient();
        await using var factory = new DevControlStage8Factory(connectionString, fakeGitHub);
        await factory.ResetDatabaseAsync();
        using var ownerClient = await factory.CreateAuthenticatedClientAsync("owner@example.com");
        var (_, organization, project, environment) = await CreateTenantAsync(ownerClient);

        _ = await PostJsonAsync<InvitationDto>(
            ownerClient,
            $"/api/organizations/{organization.Id}/invitations",
            new { email = "viewer@example.com", role = "Viewer" });
        using var viewerClient = await factory.CreateAuthenticatedClientAsync("viewer@example.com");
        var accept = await PostJsonRawAsync(viewerClient, $"/api/invitations/{factory.EmailSender.LastInvitationToken()}/accept", new { });
        Assert.Equal(HttpStatusCode.OK, accept.StatusCode);

        var denied = await PostJsonRawAsync(
            viewerClient,
            $"/api/organizations/{organization.Id}/github/onboarding-prs",
            OnboardingPayload(project.Id, environment.Id));
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);

        var pullRequest = await PostJsonAsync<OnboardingPrDto>(
            ownerClient,
            $"/api/organizations/{organization.Id}/github/onboarding-prs",
            OnboardingPayload(project.Id, environment.Id));

        Assert.Equal(17, pullRequest.PullRequestNumber);
        Assert.Contains("id-token: write", fakeGitHub.PatchedWorkflow, StringComparison.Ordinal);
        Assert.Contains("devcontrol apps register", fakeGitHub.PatchedWorkflow, StringComparison.Ordinal);
        Assert.Contains("--github-oidc-token", fakeGitHub.PatchedWorkflow, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GitHubOidcRegistration_UpsertsLiveApp_StoresRun_AndLinksConnection()
    {
        var connectionString = Environment.GetEnvironmentVariable("DEVCONTROL_TEST_CONNECTION_STRING");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return;
        }

        var fakeGitHub = new FakeGitHubAppClient();
        await using var factory = new DevControlStage8Factory(connectionString, fakeGitHub);
        await factory.ResetDatabaseAsync();
        using var ownerClient = await factory.CreateAuthenticatedClientAsync("owner@example.com");
        var (_, organization, project, environment) = await CreateTenantAsync(ownerClient);
        _ = await PostJsonAsync<OnboardingPrDto>(
            ownerClient,
            $"/api/organizations/{organization.Id}/github/onboarding-prs",
            OnboardingPayload(project.Id, environment.Id));

        using var anonymousClient = factory.CreateClient();
        var response = await anonymousClient.PostAsJsonAsync("/api/apps/register", new
        {
            repo = FakeGitHubAppClient.Repo,
            environment = environment.Slug,
            serviceUrl = "https://sample.example.com",
            healthUrl = "https://sample.example.com/health",
            commitSha = "abcdef1234567890",
            version = "v1",
            imageDigest = "sha256:v1",
            capabilities = new[] { "health", "deployment-events", "deploy", "redeploy", "rollback" },
            gitHubOidcToken = "valid"
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DevControlDbContext>();
        var liveApp = await dbContext.LiveApps.SingleAsync();
        var connection = await dbContext.GitHubRepoConnections.SingleAsync();
        var deployment = await dbContext.LiveAppDeployments.SingleAsync();
        Assert.Equal(123456789L, liveApp.GitHubRunId);
        Assert.Equal("https://github.com/fullstack-nick/devcontrol-sample-live-app/actions/runs/123456789", liveApp.GitHubRunUrl);
        Assert.Equal(liveApp.Id, connection.LiveAppId);
        Assert.Equal(123456789L, deployment.GitHubRunId);
    }

    [Fact]
    public async Task LiveControl_IsAdminOnly_AndPersistsDispatch()
    {
        var connectionString = Environment.GetEnvironmentVariable("DEVCONTROL_TEST_CONNECTION_STRING");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return;
        }

        var fakeGitHub = new FakeGitHubAppClient();
        await using var factory = new DevControlStage8Factory(connectionString, fakeGitHub);
        await factory.ResetDatabaseAsync();
        using var ownerClient = await factory.CreateAuthenticatedClientAsync("owner@example.com");
        var (_, organization, project, environment) = await CreateTenantAsync(ownerClient);
        _ = await PostJsonAsync<OnboardingPrDto>(
            ownerClient,
            $"/api/organizations/{organization.Id}/github/onboarding-prs",
            OnboardingPayload(project.Id, environment.Id));

        using var anonymousClient = factory.CreateClient();
        _ = await anonymousClient.PostAsJsonAsync("/api/apps/register", new
        {
            repo = FakeGitHubAppClient.Repo,
            environment = environment.Slug,
            serviceUrl = "https://sample.example.com",
            healthUrl = "https://sample.example.com/health",
            commitSha = "abcdef1234567890",
            version = "v1",
            imageDigest = "sha256:v1",
            capabilities = new[] { "health", "deployment-events", "deploy", "redeploy", "rollback" },
            gitHubOidcToken = "valid"
        });
        var app = (await ownerClient.GetFromJsonAsync<List<LiveAppDto>>($"/api/organizations/{organization.Id}/apps"))!.Single();

        _ = await PostJsonAsync<InvitationDto>(
            ownerClient,
            $"/api/organizations/{organization.Id}/invitations",
            new { email = "viewer@example.com", role = "Viewer" });
        using var viewerClient = await factory.CreateAuthenticatedClientAsync("viewer@example.com");
        var accept = await PostJsonRawAsync(viewerClient, $"/api/invitations/{factory.EmailSender.LastInvitationToken()}/accept", new { });
        Assert.Equal(HttpStatusCode.OK, accept.StatusCode);

        var denied = await PostJsonRawAsync(
            viewerClient,
            $"/api/organizations/{organization.Id}/apps/{app.Id}/actions/deploy",
            new { reason = "viewer attempt" });
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);

        var accepted = await PostJsonRawAsync(
            ownerClient,
            $"/api/organizations/{organization.Id}/apps/{app.Id}/actions/deploy",
            new { reason = "stage 8 proof" });
        Assert.Equal(HttpStatusCode.Accepted, accepted.StatusCode);

        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DevControlDbContext>();
        var dispatch = await dbContext.GitHubWorkflowDispatches.SingleAsync();
        var controlAction = await dbContext.ControlActions.SingleAsync(action => action.Id == dispatch.ControlActionId);
        Assert.Equal("deploy", dispatch.Action);
        Assert.Equal(987654321L, dispatch.GitHubRunId);
        Assert.Equal("InProgress", controlAction.Status.ToString());
        Assert.Equal("deploy", fakeGitHub.LastDispatchInputs["devcontrol_action"]);
    }

    [Fact]
    public async Task LiveControl_ReturnsWorkflowUrlImmediatelyWhenRunIdIsNotVisibleYet()
    {
        var connectionString = Environment.GetEnvironmentVariable("DEVCONTROL_TEST_CONNECTION_STRING");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return;
        }

        var fakeGitHub = new FakeGitHubAppClient
        {
            DispatchInfo = new GitHubWorkflowDispatchInfo(null, string.Empty)
        };
        await using var factory = new DevControlStage8Factory(connectionString, fakeGitHub);
        await factory.ResetDatabaseAsync();
        using var ownerClient = await factory.CreateAuthenticatedClientAsync("owner@example.com");
        var (_, organization, project, environment) = await CreateTenantAsync(ownerClient);
        _ = await PostJsonAsync<OnboardingPrDto>(
            ownerClient,
            $"/api/organizations/{organization.Id}/github/onboarding-prs",
            OnboardingPayload(project.Id, environment.Id));

        using var anonymousClient = factory.CreateClient();
        _ = await anonymousClient.PostAsJsonAsync("/api/apps/register", new
        {
            repo = FakeGitHubAppClient.Repo,
            environment = environment.Slug,
            serviceUrl = "https://sample.example.com",
            healthUrl = "https://sample.example.com/health",
            commitSha = "abcdef1234567890",
            version = "v1",
            imageDigest = "sha256:v1",
            capabilities = new[] { "health", "deployment-events", "deploy", "redeploy", "rollback" },
            gitHubOidcToken = "valid"
        });
        var app = (await ownerClient.GetFromJsonAsync<List<LiveAppDto>>($"/api/organizations/{organization.Id}/apps"))!.Single();

        var accepted = await PostJsonAsync<WorkflowDispatchDto>(
            ownerClient,
            $"/api/organizations/{organization.Id}/apps/{app.Id}/actions/redeploy",
            new { reason = "verify immediate run link" });

        Assert.Equal($"https://github.com/{FakeGitHubAppClient.Repo}/actions/workflows/deploy.yml", accepted.RunUrl);

        fakeGitHub.WorkflowRunToFind = new GitHubWorkflowRunInfo(
            123456789,
            $"https://github.com/{FakeGitHubAppClient.Repo}/actions/runs/123456789",
            "completed",
            "success",
            DateTimeOffset.UtcNow);
        var synced = await PostJsonAsync<List<WorkflowDispatchDto>>(
            ownerClient,
            $"/api/organizations/{organization.Id}/github/workflow-dispatches/sync",
            new { });
        var syncedDispatch = synced.Single();
        Assert.Equal("Succeeded", syncedDispatch.ControlActionStatus);
        Assert.Equal("completed", syncedDispatch.Status);
        Assert.Equal("success", syncedDispatch.Conclusion);
        Assert.Equal($"https://github.com/{FakeGitHubAppClient.Repo}/actions/runs/123456789", syncedDispatch.RunUrl);

        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DevControlDbContext>();
        var dispatch = await dbContext.GitHubWorkflowDispatches.SingleAsync();
        Assert.Equal(123456789, dispatch.GitHubRunId);
        Assert.Equal($"https://github.com/{FakeGitHubAppClient.Repo}/actions/runs/123456789", dispatch.RunUrl);
    }

    private static object OnboardingPayload(Guid projectId, Guid environmentId)
    {
        return new
        {
            projectId,
            environmentId,
            repo = FakeGitHubAppClient.Repo,
            workflowPath = ".github/workflows/deploy.yml",
            jobId = "deploy",
            serviceUrlExpression = "https://sample.example.com",
            healthUrlExpression = "https://sample.example.com/health",
            versionExpression = "${{ github.sha }}",
            imageDigestExpression = "sha256:test",
            capabilities = new[] { "health", "deployment-events", "deploy", "redeploy", "rollback" }
        };
    }

    private static async Task<(MeDto Me, OrganizationDto Organization, ProjectDto Project, EnvironmentDto Environment)> CreateTenantAsync(HttpClient client)
    {
        var organization = await PostJsonAsync<OrganizationDto>(
            client,
            "/api/organizations",
            new { name = "Acme Platform", slug = "acme-platform" });
        var project = await PostJsonAsync<ProjectDto>(
            client,
            $"/api/organizations/{organization.Id}/projects",
            new { name = "Sample App", slug = "sample-app", description = "Stage 8 sample" });
        var environment = await PostJsonAsync<EnvironmentDto>(
            client,
            $"/api/organizations/{organization.Id}/projects/{project.Id}/environments",
            new { name = "Production", slug = "production" });
        var me = await client.GetFromJsonAsync<MeDto>("/api/auth/me") ?? throw new InvalidOperationException("Missing me response.");
        return (me, organization, project, environment);
    }

    private static async Task<T> PostJsonAsync<T>(HttpClient client, string path, object payload)
    {
        var response = await PostJsonRawAsync(client, path, payload);
        response.EnsureSuccessStatusCode();
        var value = await response.Content.ReadFromJsonAsync<T>();
        return value ?? throw new InvalidOperationException($"Response from {path} was empty.");
    }

    private static async Task<HttpResponseMessage> PostJsonRawAsync(HttpClient client, string path, object payload)
    {
        var csrf = await client.GetFromJsonAsync<CsrfDto>("/api/auth/csrf");
        using var request = new HttpRequestMessage(HttpMethod.Post, path)
        {
            Content = JsonContent.Create(payload)
        };
        request.Headers.Add("X-CSRF-TOKEN", csrf?.Token ?? throw new InvalidOperationException("Missing CSRF token."));
        return await client.SendAsync(request);
    }

    private sealed class DevControlStage8Factory : WebApplicationFactory<Program>
    {
        private readonly string? originalConnectionString;
        private readonly FakeGitHubAppClient gitHubAppClient;

        public DevControlStage8Factory(string connectionString, FakeGitHubAppClient gitHubAppClient)
        {
            originalConnectionString = Environment.GetEnvironmentVariable("ConnectionStrings__DevControl");
            Environment.SetEnvironmentVariable("ConnectionStrings__DevControl", connectionString);
            this.gitHubAppClient = gitHubAppClient;
        }

        public RecordingEmailSender EmailSender { get; } = new();

        public async Task ResetDatabaseAsync()
        {
            await using var scope = Services.CreateAsyncScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<DevControlDbContext>();
            await dbContext.Database.ExecuteSqlRawAsync("""
                DROP TABLE IF EXISTS
                    github_workflow_dispatches,
                    github_onboarding_pull_requests,
                    github_repo_connections,
                    github_installations,
                    incident_monitors,
                    incident_updates,
                    monitor_checks,
                    status_releases,
                    incidents,
                    uptime_monitors,
                    webhook_delivery_attempts,
                    webhook_deliveries,
                    webhook_events,
                    webhook_endpoints,
                    feature_flag_changes,
                    feature_flags,
                    api_key_rate_limit_windows,
                    api_key_usage_daily,
                    api_keys,
                    live_app_deployments,
                    live_apps,
                    registration_tokens,
                    audit_logs,
                    control_actions,
                    environments,
                    projects,
                    organization_invitations,
                    organization_members,
                    organizations,
                    users,
                    data_protection_keys,
                    schema_versions,
                    "__EFMigrationsHistory"
                CASCADE;
                """);
            await dbContext.Database.MigrateAsync();
        }

        public async Task<HttpClient> CreateAuthenticatedClientAsync(string email)
        {
            var client = CreateClient(new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false,
                HandleCookies = true
            });

            var response = await client.GetAsync($"/auth/login?email={WebUtility.UrlEncode(email)}");
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            return client;
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Test");
            builder.ConfigureTestServices(services =>
            {
                foreach (var descriptor in services.Where(descriptor =>
                             descriptor.ServiceType == typeof(IEmailSender) ||
                             descriptor.ServiceType == typeof(IGitHubAppClient) ||
                             descriptor.ServiceType == typeof(IGitHubOidcTokenValidator)).ToList())
                {
                    services.Remove(descriptor);
                }

                services.AddSingleton<IEmailSender>(EmailSender);
                services.AddSingleton<IGitHubAppClient>(gitHubAppClient);
                services.AddSingleton<IGitHubOidcTokenValidator>(new FakeGitHubOidcTokenValidator());
            });
        }

        protected override void Dispose(bool disposing)
        {
            Environment.SetEnvironmentVariable("ConnectionStrings__DevControl", originalConnectionString);
            base.Dispose(disposing);
        }
    }

    private sealed class FakeGitHubAppClient : IGitHubAppClient
    {
        public const string Repo = "fullstack-nick/devcontrol-sample-live-app";

        public bool IsConfigured => true;

        public string PatchedWorkflow { get; private set; } = string.Empty;

        public IReadOnlyDictionary<string, string> LastDispatchInputs { get; private set; } = new Dictionary<string, string>();

        public GitHubWorkflowDispatchInfo DispatchInfo { get; init; } = new(987654321, $"https://github.com/{Repo}/actions/runs/987654321");

        public GitHubWorkflowRunInfo? WorkflowRunToFind { get; set; }

        public Task<GitHubInstallationInfo?> GetRepositoryInstallationAsync(GitHubRepoName repo, CancellationToken cancellationToken)
        {
            return Task.FromResult<GitHubInstallationInfo?>(new GitHubInstallationInfo(123, "fullstack-nick", "User", "selected", "{}"));
        }

        public Task<GitHubRepositoryInfo> GetRepositoryAsync(GitHubRepoName repo, long installationId, CancellationToken cancellationToken)
        {
            return Task.FromResult(new GitHubRepositoryInfo(Repo, "main", $"https://github.com/{Repo}"));
        }

        public Task<IReadOnlyList<GitHubWorkflowInfo>> ListWorkflowsAsync(GitHubRepoName repo, long installationId, CancellationToken cancellationToken)
        {
            return Task.FromResult<IReadOnlyList<GitHubWorkflowInfo>>([new GitHubWorkflowInfo(10, "Deploy", ".github/workflows/deploy.yml", "active")]);
        }

        public Task<GitHubFileContent> GetFileContentAsync(GitHubRepoName repo, long installationId, string path, string gitRef, CancellationToken cancellationToken)
        {
            const string workflow = """
                                    name: Deploy
                                    on:
                                      workflow_dispatch:
                                    jobs:
                                      deploy:
                                        runs-on: ubuntu-latest
                                        steps:
                                          - uses: actions/checkout@v4
                                    """;
            return Task.FromResult(new GitHubFileContent(path, "file-sha", workflow));
        }

        public Task<GitHubPullRequestInfo> CreateOnboardingPullRequestAsync(
            GitHubRepoName repo,
            long installationId,
            string baseBranch,
            string headBranch,
            string workflowPath,
            string currentFileSha,
            string patchedContent,
            string title,
            string body,
            CancellationToken cancellationToken)
        {
            PatchedWorkflow = patchedContent;
            return Task.FromResult(new GitHubPullRequestInfo(17, $"https://github.com/{Repo}/pull/17"));
        }

        public Task<GitHubPullRequestState?> GetPullRequestAsync(GitHubRepoName repo, long installationId, int pullRequestNumber, CancellationToken cancellationToken)
        {
            return Task.FromResult<GitHubPullRequestState?>(new GitHubPullRequestState("closed", true, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow));
        }

        public Task<GitHubWorkflowDispatchInfo> DispatchWorkflowAsync(
            GitHubRepoName repo,
            long installationId,
            string workflowPath,
            string gitRef,
            IReadOnlyDictionary<string, string> inputs,
            DateTimeOffset requestedAt,
            CancellationToken cancellationToken)
        {
            LastDispatchInputs = new Dictionary<string, string>(inputs);
            return Task.FromResult(DispatchInfo);
        }

        public Task<GitHubWorkflowRunInfo?> GetWorkflowRunAsync(GitHubRepoName repo, long installationId, long runId, CancellationToken cancellationToken)
        {
            return Task.FromResult<GitHubWorkflowRunInfo?>(new GitHubWorkflowRunInfo(runId, $"https://github.com/{Repo}/actions/runs/{runId}", "completed", "success", DateTimeOffset.UtcNow));
        }

        public Task<GitHubWorkflowRunInfo?> FindWorkflowRunAsync(GitHubRepoName repo, long installationId, string workflowPath, string gitRef, DateTimeOffset requestedAt, CancellationToken cancellationToken)
        {
            return Task.FromResult(WorkflowRunToFind);
        }
    }

    private sealed class FakeGitHubOidcTokenValidator : IGitHubOidcTokenValidator
    {
        public Task<GitHubOidcClaims?> ValidateAsync(string token, string expectedAudience, CancellationToken cancellationToken)
        {
            return Task.FromResult(token == "valid"
                ? new GitHubOidcClaims(
                    FakeGitHubAppClient.Repo,
                    "refs/heads/main",
                    $"{FakeGitHubAppClient.Repo}/.github/workflows/deploy.yml@refs/heads/main",
                    "abcdef1234567890",
                    "123456789",
                    "fullstack-nick",
                    "workflow_dispatch")
                : null);
        }
    }

    private sealed class RecordingEmailSender : IEmailSender
    {
        public ConcurrentQueue<EmailMessage> Messages { get; } = new();

        public Task SendAsync(EmailMessage message, CancellationToken cancellationToken)
        {
            Messages.Enqueue(message);
            return Task.CompletedTask;
        }

        public string LastInvitationToken()
        {
            var message = Messages.Last();
            var match = InvitationLinkRegex().Match(message.TextBody);
            if (!match.Success)
            {
                throw new InvalidOperationException("Invitation email did not contain an invitation link.");
            }

            return WebUtility.UrlDecode(match.Groups["token"].Value);
        }
    }

    [GeneratedRegex("/invitations/(?<token>[^\\s]+)")]
    private static partial Regex InvitationLinkRegex();

    private sealed record CsrfDto(string Token);

    private sealed record MeDto(UserDto User, List<OrganizationDto> Organizations);

    private sealed record UserDto(Guid Id, string Email, string DisplayName);

    private sealed record OrganizationDto(Guid Id, string Name, string Slug, string Role);

    private sealed record ProjectDto(Guid Id, string OrganizationId, string Name, string Slug, string Description);

    private sealed record EnvironmentDto(Guid Id, string ProjectId, string Name, string Slug);

    private sealed record InvitationDto(Guid Id, string Email, string Role, string Status);

    private sealed record OnboardingPrDto(Guid Id, int PullRequestNumber, string PullRequestUrl);

    private sealed record LiveAppDto(Guid Id);

    private sealed record WorkflowDispatchDto(Guid Id, string ControlActionStatus, string RunUrl, string Status, string Conclusion);
}
