using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using DevControl.Application.Email;
using DevControl.Infrastructure.Database;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace DevControl.IntegrationTests;

[Collection(PostgresIntegrationCollection.Name)]
public sealed partial class AppRegistryEndpointTests
{
    [Fact]
    public async Task AdminCanCreateToken_ViewerCannot_AndSnippetIsReturned()
    {
        var connectionString = Environment.GetEnvironmentVariable("DEVCONTROL_TEST_CONNECTION_STRING");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return;
        }

        await using var factory = new DevControlStage3Factory(connectionString);
        await factory.ResetDatabaseAsync();
        using var ownerClient = await factory.CreateAuthenticatedClientAsync("owner@example.com");
        var (_, organization, project, environment) = await CreateTenantAsync(ownerClient);

        var token = await PostJsonAsync<RegistrationTokenCreateDto>(
            ownerClient,
            $"/api/organizations/{organization.Id}/projects/{project.Id}/environments/{environment.Id}/registration-tokens",
            new { name = "Production deploys" });

        Assert.StartsWith("dcr_", token.Secret, StringComparison.Ordinal);
        Assert.Contains("devcontrol apps register", token.WorkflowSnippet, StringComparison.Ordinal);
        Assert.Contains("--environment production", token.WorkflowSnippet, StringComparison.Ordinal);

        _ = await PostJsonAsync<InvitationDto>(
            ownerClient,
            $"/api/organizations/{organization.Id}/invitations",
            new { email = "viewer@example.com", role = "Viewer" });
        using var viewerClient = await factory.CreateAuthenticatedClientAsync("viewer@example.com");
        var accept = await PostJsonRawAsync(viewerClient, $"/api/invitations/{factory.EmailSender.LastInvitationToken()}/accept", new { });
        Assert.Equal(HttpStatusCode.OK, accept.StatusCode);

        var denied = await PostJsonRawAsync(
            viewerClient,
            $"/api/organizations/{organization.Id}/projects/{project.Id}/environments/{environment.Id}/registration-tokens",
            new { name = "Denied" });
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
    }

    [Fact]
    public async Task RegisterApp_RequiresToken_UpsertsLiveApp_AppendsDeployment_AndHonorsRevocation()
    {
        var connectionString = Environment.GetEnvironmentVariable("DEVCONTROL_TEST_CONNECTION_STRING");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return;
        }

        await using var factory = new DevControlStage3Factory(connectionString);
        await factory.ResetDatabaseAsync();
        using var ownerClient = await factory.CreateAuthenticatedClientAsync("owner@example.com");
        var (_, organization, project, environment) = await CreateTenantAsync(ownerClient);
        var token = await PostJsonAsync<RegistrationTokenCreateDto>(
            ownerClient,
            $"/api/organizations/{organization.Id}/projects/{project.Id}/environments/{environment.Id}/registration-tokens",
            new { name = "Production deploys" });

        using var anonymousClient = factory.CreateClient();
        var missingToken = await anonymousClient.PostAsJsonAsync("/api/apps/register", RegistrationPayload("v1", environment.Slug));
        Assert.Equal(HttpStatusCode.Unauthorized, missingToken.StatusCode);

        var first = await RegisterAsync(anonymousClient, token.Secret, RegistrationPayload("v1", environment.Slug));
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        var second = await RegisterAsync(anonymousClient, token.Secret, RegistrationPayload("v2", environment.Slug));
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<DevControlDbContext>();
            Assert.Equal(1, await dbContext.LiveApps.CountAsync());
            Assert.Equal(2, await dbContext.LiveAppDeployments.CountAsync());
            Assert.Contains(await dbContext.AuditLogs.ToListAsync(), auditLog => auditLog.Action == "app.register");
            Assert.NotNull((await dbContext.RegistrationTokens.SingleAsync()).LastUsedAt);
        }

        await PostJsonAsync<object>(
            ownerClient,
            $"/api/organizations/{organization.Id}/registration-tokens/{token.Id}/revoke",
            new { });
        var revoked = await RegisterAsync(anonymousClient, token.Secret, RegistrationPayload("v3", environment.Slug));
        Assert.Equal(HttpStatusCode.Unauthorized, revoked.StatusCode);
    }

    [Fact]
    public async Task RegisterApp_RejectsWrongEnvironmentForToken()
    {
        var connectionString = Environment.GetEnvironmentVariable("DEVCONTROL_TEST_CONNECTION_STRING");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return;
        }

        await using var factory = new DevControlStage3Factory(connectionString);
        await factory.ResetDatabaseAsync();
        using var ownerClient = await factory.CreateAuthenticatedClientAsync("owner@example.com");
        var (_, organization, project, environment) = await CreateTenantAsync(ownerClient);
        var token = await PostJsonAsync<RegistrationTokenCreateDto>(
            ownerClient,
            $"/api/organizations/{organization.Id}/projects/{project.Id}/environments/{environment.Id}/registration-tokens",
            new { name = "Production deploys" });

        using var anonymousClient = factory.CreateClient();
        var response = await RegisterAsync(anonymousClient, token.Secret, RegistrationPayload("v1", "staging"));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    private static object RegistrationPayload(string version, string environment)
    {
        return new
        {
            repo = "fullstack-nick/sample-app",
            environment,
            serviceUrl = "https://sample.example.com",
            healthUrl = "https://sample.example.com/health",
            commitSha = "abcdef1234567890",
            version,
            imageDigest = $"sha256:{version}",
            capabilities = new[] { "health", "deployment-events" }
        };
    }

    private static async Task<HttpResponseMessage> RegisterAsync(HttpClient client, string token, object payload)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/apps/register")
        {
            Content = JsonContent.Create(payload)
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await client.SendAsync(request);
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
            new { name = "Sample App", slug = "sample-app", description = "Stage 3 sample" });
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

    private sealed class DevControlStage3Factory : WebApplicationFactory<Program>
    {
        private readonly string? originalConnectionString;

        public DevControlStage3Factory(string connectionString)
        {
            originalConnectionString = Environment.GetEnvironmentVariable("ConnectionStrings__DevControl");
            Environment.SetEnvironmentVariable("ConnectionStrings__DevControl", connectionString);
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
                var descriptors = services
                    .Where(descriptor => descriptor.ServiceType == typeof(IEmailSender))
                    .ToList();

                foreach (var descriptor in descriptors)
                {
                    services.Remove(descriptor);
                }

                services.AddSingleton<IEmailSender>(EmailSender);
            });
        }

        protected override void Dispose(bool disposing)
        {
            Environment.SetEnvironmentVariable("ConnectionStrings__DevControl", originalConnectionString);
            base.Dispose(disposing);
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
            if (!Messages.TryPeek(out _))
            {
                throw new InvalidOperationException("No invitation email was recorded.");
            }

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

    private sealed record RegistrationTokenCreateDto(Guid Id, string Secret, string WorkflowSnippet);
}
