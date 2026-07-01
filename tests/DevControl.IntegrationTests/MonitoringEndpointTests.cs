using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using DevControl.Application.Email;
using DevControl.Application.Outbound;
using DevControl.Infrastructure.Database;
using DevControl.Infrastructure.Outbound;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace DevControl.IntegrationTests;

[Collection(PostgresIntegrationCollection.Name)]
public sealed partial class MonitoringEndpointTests
{
    [Fact]
    public async Task SchedulerCreatesAndResolvesIncident_PublicStatusAndReleaseReflectState_AndWebhooksPublish()
    {
        var connectionString = Environment.GetEnvironmentVariable("DEVCONTROL_TEST_CONNECTION_STRING");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return;
        }

        await using var factory = new DevControlStage7Factory(connectionString);
        await factory.ResetDatabaseAsync();
        using var ownerClient = await factory.CreateAuthenticatedClientAsync("owner@example.com");
        var (_, organization, project, environment) = await CreateTenantAsync(ownerClient);

        _ = await PostJsonAsync<WebhookEndpointDto>(
            ownerClient,
            $"/api/organizations/{organization.Id}/projects/{project.Id}/environments/{environment.Id}/webhook-endpoints",
            new
            {
                name = "Status Receiver",
                url = "https://hooks.example.com/devcontrol",
                eventTypes = new[] { "monitor.down", "incident.created", "monitor.recovered", "incident.resolved", "release.published" }
            });

        var token = await PostJsonAsync<RegistrationTokenCreateDto>(
            ownerClient,
            $"/api/organizations/{organization.Id}/projects/{project.Id}/environments/{environment.Id}/registration-tokens",
            new { name = "Production deploys" });

        using var anonymousClient = factory.CreateClient();
        using var registerRequest = new HttpRequestMessage(HttpMethod.Post, "/api/apps/register")
        {
            Content = JsonContent.Create(new
            {
                repo = "fullstack-nick/sample-app",
                environment = environment.Slug,
                serviceUrl = "https://sample.example.com",
                healthUrl = "https://sample.example.com/health",
                commitSha = "abcdef1234567890",
                version = "v7",
                imageDigest = "sha256:v7",
                capabilities = new[] { "health", "deployment-events" }
            })
        };
        registerRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.Secret);
        (await anonymousClient.SendAsync(registerRequest)).EnsureSuccessStatusCode();

        var monitors = await ownerClient.GetFromJsonAsync<List<MonitorDto>>($"/api/organizations/{organization.Id}/monitors");
        var monitor = Assert.Single(monitors!);
        Assert.Equal("Unknown", monitor.CurrentStatus);
        Assert.Equal("https://sample.example.com/health", monitor.Url);

        factory.Outbound.Enqueue(new SafeOutboundResponse(SafeOutboundResultKind.Completed, HttpStatusCode.InternalServerError, "down", false, 4, null, TimeSpan.FromMilliseconds(5)));
        factory.Outbound.Enqueue(new SafeOutboundResponse(SafeOutboundResultKind.Completed, HttpStatusCode.OK, "monitor down", false, 12, null, TimeSpan.FromMilliseconds(5)));
        factory.Outbound.Enqueue(new SafeOutboundResponse(SafeOutboundResultKind.Completed, HttpStatusCode.OK, "incident created", false, 16, null, TimeSpan.FromMilliseconds(5)));
        await RunSchedulerAsync(ownerClient);

        monitors = await ownerClient.GetFromJsonAsync<List<MonitorDto>>($"/api/organizations/{organization.Id}/monitors");
        Assert.Equal("Down", Assert.Single(monitors!).CurrentStatus);
        var checks = await ownerClient.GetFromJsonAsync<List<MonitorCheckDto>>($"/api/organizations/{organization.Id}/monitors/{monitor.Id}/checks");
        Assert.Equal("Down", Assert.Single(checks!).Status);
        var incidents = await ownerClient.GetFromJsonAsync<List<IncidentDto>>($"/api/organizations/{organization.Id}/incidents");
        var incident = Assert.Single(incidents!);
        Assert.Equal("Investigating", incident.Status);

        var downStatus = await ownerClient.GetFromJsonAsync<PublicStatusDto>("/api/public/status/acme-platform/sample-app?environment=production");
        Assert.Equal("down", downStatus!.OverallStatus);
        Assert.Single(downStatus.Incidents);
        Assert.Single(downStatus.Incidents[0].Updates);

        await MakeMonitorDueAsync(factory, monitor.Id);
        factory.Outbound.Enqueue(new SafeOutboundResponse(SafeOutboundResultKind.Completed, HttpStatusCode.OK, "ok", false, 2, null, TimeSpan.FromMilliseconds(4)));
        factory.Outbound.Enqueue(new SafeOutboundResponse(SafeOutboundResultKind.Completed, HttpStatusCode.OK, "monitor recovered", false, 18, null, TimeSpan.FromMilliseconds(5)));
        factory.Outbound.Enqueue(new SafeOutboundResponse(SafeOutboundResultKind.Completed, HttpStatusCode.OK, "incident resolved", false, 18, null, TimeSpan.FromMilliseconds(5)));
        await RunSchedulerAsync(ownerClient);

        incidents = await ownerClient.GetFromJsonAsync<List<IncidentDto>>($"/api/organizations/{organization.Id}/incidents");
        Assert.Equal("Resolved", Assert.Single(incidents!).Status);
        var recoveredStatus = await ownerClient.GetFromJsonAsync<PublicStatusDto>("/api/public/status/acme-platform/sample-app?environment=production");
        Assert.Equal("operational", recoveredStatus!.OverallStatus);

        var release = await PostJsonAsync<ReleaseDto>(
            ownerClient,
            $"/api/organizations/{organization.Id}/projects/{project.Id}/environments/{environment.Id}/releases",
            new { title = "Stage 7 proof", version = "v7", body = "Monitoring and incidents are live." });
        factory.Outbound.Enqueue(new SafeOutboundResponse(SafeOutboundResultKind.Completed, HttpStatusCode.OK, "release", false, 7, null, TimeSpan.FromMilliseconds(5)));
        release = await PostJsonAsync<ReleaseDto>(
            ownerClient,
            $"/api/organizations/{organization.Id}/releases/{release.Id}/publish",
            new { });
        Assert.Equal("Published", release.Status);
        await RunSchedulerAsync(ownerClient);

        var publishedStatus = await ownerClient.GetFromJsonAsync<PublicStatusDto>("/api/public/status/acme-platform/sample-app?environment=production");
        Assert.Contains(publishedStatus!.Releases, candidate => candidate.Version == "v7");

        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DevControlDbContext>();
        Assert.Contains(await dbContext.WebhookEvents.ToListAsync(), webhookEvent => webhookEvent.EventType == "monitor.down");
        Assert.Contains(await dbContext.WebhookEvents.ToListAsync(), webhookEvent => webhookEvent.EventType == "incident.created");
        Assert.Contains(await dbContext.WebhookEvents.ToListAsync(), webhookEvent => webhookEvent.EventType == "incident.resolved");
        Assert.Contains(await dbContext.WebhookEvents.ToListAsync(), webhookEvent => webhookEvent.EventType == "release.published");
        Assert.True(await dbContext.WebhookDeliveryAttempts.CountAsync(attempt => attempt.Succeeded) >= 5);
    }

    [Fact]
    public async Task ViewerCannotMutateMonitor_AndPrivateMonitorUrlIsRejected()
    {
        var connectionString = Environment.GetEnvironmentVariable("DEVCONTROL_TEST_CONNECTION_STRING");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return;
        }

        await using var factory = new DevControlStage7Factory(connectionString);
        await factory.ResetDatabaseAsync();
        using var ownerClient = await factory.CreateAuthenticatedClientAsync("owner@example.com");
        var (_, organization, project, environment) = await CreateTenantAsync(ownerClient);
        var token = await PostJsonAsync<RegistrationTokenCreateDto>(
            ownerClient,
            $"/api/organizations/{organization.Id}/projects/{project.Id}/environments/{environment.Id}/registration-tokens",
            new { name = "Production deploys" });

        using var anonymousClient = factory.CreateClient();
        using var registerRequest = new HttpRequestMessage(HttpMethod.Post, "/api/apps/register")
        {
            Content = JsonContent.Create(new
            {
                repo = "fullstack-nick/sample-app",
                environment = environment.Slug,
                serviceUrl = "https://sample.example.com",
                healthUrl = "https://sample.example.com/health",
                commitSha = "abcdef1234567890",
                version = "v7",
                imageDigest = "sha256:v7",
                capabilities = new[] { "health" }
            })
        };
        registerRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.Secret);
        (await anonymousClient.SendAsync(registerRequest)).EnsureSuccessStatusCode();
        var monitor = Assert.Single(await ownerClient.GetFromJsonAsync<List<MonitorDto>>($"/api/organizations/{organization.Id}/monitors") ?? []);

        _ = await PostJsonAsync<InvitationDto>(
            ownerClient,
            $"/api/organizations/{organization.Id}/invitations",
            new { email = "viewer@example.com", role = "Viewer" });
        using var viewerClient = await factory.CreateAuthenticatedClientAsync("viewer@example.com");
        var accept = await PostJsonRawAsync(viewerClient, $"/api/invitations/{factory.EmailSender.LastInvitationToken()}/accept", new { });
        Assert.Equal(HttpStatusCode.OK, accept.StatusCode);

        var denied = await PatchJsonRawAsync(
            viewerClient,
            $"/api/organizations/{organization.Id}/monitors/{monitor.Id}",
            new
            {
                name = monitor.Name,
                url = monitor.Url,
                intervalSeconds = 300,
                timeoutSeconds = 5,
                slowThresholdMilliseconds = 2000,
                failureThreshold = 1,
                recoveryThreshold = 1
            });
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);

        var blocked = await PatchJsonRawAsync(
            ownerClient,
            $"/api/organizations/{organization.Id}/monitors/{monitor.Id}",
            new
            {
                name = monitor.Name,
                url = "https://127.0.0.1/health",
                intervalSeconds = 300,
                timeoutSeconds = 5,
                slowThresholdMilliseconds = 2000,
                failureThreshold = 1,
                recoveryThreshold = 1
            });
        Assert.Equal(HttpStatusCode.BadRequest, blocked.StatusCode);
    }

    private static async Task RunSchedulerAsync(HttpClient client)
    {
        using var schedulerRequest = new HttpRequestMessage(HttpMethod.Post, "/internal/scheduler/tick");
        schedulerRequest.Headers.Add("X-DevControl-Scheduler-Secret", DevControlStage7Factory.SchedulerSecret);
        var schedulerResponse = await client.SendAsync(schedulerRequest);
        schedulerResponse.EnsureSuccessStatusCode();
    }

    private static async Task MakeMonitorDueAsync(DevControlStage7Factory factory, Guid monitorId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DevControlDbContext>();
        await dbContext.UptimeMonitors
            .Where(monitor => monitor.Id == monitorId)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(monitor => monitor.NextCheckAt, DateTimeOffset.UtcNow.AddSeconds(-1)));
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
            new { name = "Sample App", slug = "sample-app", description = "Stage 7 sample" });
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

    private static async Task<HttpResponseMessage> PatchJsonRawAsync(HttpClient client, string path, object payload)
    {
        var csrf = await client.GetFromJsonAsync<CsrfDto>("/api/auth/csrf");
        using var request = new HttpRequestMessage(HttpMethod.Patch, path)
        {
            Content = JsonContent.Create(payload)
        };
        request.Headers.Add("X-CSRF-TOKEN", csrf?.Token ?? throw new InvalidOperationException("Missing CSRF token."));
        return await client.SendAsync(request);
    }

    private sealed class DevControlStage7Factory : WebApplicationFactory<Program>
    {
        public const string SchedulerSecret = "test-scheduler-secret";
        private readonly string? originalConnectionString;
        private readonly string? originalSchedulerSecret;

        public DevControlStage7Factory(string connectionString)
        {
            originalConnectionString = Environment.GetEnvironmentVariable("ConnectionStrings__DevControl");
            originalSchedulerSecret = Environment.GetEnvironmentVariable("DEVCONTROL_SCHEDULER_SECRET");
            Environment.SetEnvironmentVariable("ConnectionStrings__DevControl", connectionString);
            Environment.SetEnvironmentVariable("DEVCONTROL_SCHEDULER_SECRET", SchedulerSecret);
        }

        public RecordingEmailSender EmailSender { get; } = new();

        public RecordingSafeOutboundHttpClient Outbound { get; } = new();

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
                RemoveAll<IEmailSender>(services);
                RemoveAll<IOutboundDnsResolver>(services);
                RemoveAll<ISafeOutboundHttpClient>(services);

                services.AddSingleton<IEmailSender>(EmailSender);
                services.AddSingleton<IOutboundDnsResolver>(new StaticDnsResolver(IPAddress.Parse("93.184.216.34")));
                services.AddSingleton<ISafeOutboundHttpClient>(Outbound);
            });
        }

        protected override void Dispose(bool disposing)
        {
            Environment.SetEnvironmentVariable("ConnectionStrings__DevControl", originalConnectionString);
            Environment.SetEnvironmentVariable("DEVCONTROL_SCHEDULER_SECRET", originalSchedulerSecret);
            base.Dispose(disposing);
        }

        private static void RemoveAll<T>(IServiceCollection services)
        {
            var descriptors = services
                .Where(descriptor => descriptor.ServiceType == typeof(T))
                .ToList();

            foreach (var descriptor in descriptors)
            {
                services.Remove(descriptor);
            }
        }
    }

    private sealed class RecordingSafeOutboundHttpClient : ISafeOutboundHttpClient
    {
        private readonly ConcurrentQueue<SafeOutboundResponse> responses = new();

        public ConcurrentQueue<SafeOutboundRequest> Requests { get; } = new();

        public void Enqueue(SafeOutboundResponse response)
        {
            responses.Enqueue(response);
        }

        public Task<SafeOutboundResponse> SendAsync(SafeOutboundRequest request, CancellationToken cancellationToken)
        {
            Requests.Enqueue(request);
            if (!responses.TryDequeue(out var response))
            {
                throw new InvalidOperationException("No fake outbound response was queued.");
            }

            return Task.FromResult(response);
        }
    }

    private sealed class StaticDnsResolver(IPAddress address) : IOutboundDnsResolver
    {
        public Task<IReadOnlyList<IPAddress>> ResolveAsync(string host, CancellationToken cancellationToken)
        {
            if (IPAddress.TryParse(host, out var parsed))
            {
                return Task.FromResult<IReadOnlyList<IPAddress>>([parsed]);
            }

            return Task.FromResult<IReadOnlyList<IPAddress>>([address]);
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

    private sealed record RegistrationTokenCreateDto(Guid Id, string Secret);

    private sealed record WebhookEndpointDto(Guid Id, string Name);

    private sealed record MonitorDto(Guid Id, string Name, string Url, string CurrentStatus);

    private sealed record MonitorCheckDto(Guid Id, string Status);

    private sealed record IncidentDto(Guid Id, string Status);

    private sealed record ReleaseDto(Guid Id, string Version, string Status);

    private sealed record PublicStatusDto(string OverallStatus, List<PublicIncidentDto> Incidents, List<PublicReleaseDto> Releases);

    private sealed record PublicIncidentDto(Guid Id, string Status, List<IncidentUpdateDto> Updates);

    private sealed record IncidentUpdateDto(Guid Id, string Visibility);

    private sealed record PublicReleaseDto(Guid Id, string Version);
}
