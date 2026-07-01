using System.Collections.Concurrent;
using System.Net;
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
public sealed partial class WebhookEndpointTests
{
    [Fact]
    public async Task AdminCanCreateTestPauseResumeRetryAndSchedulerDeliversRealEvents()
    {
        var connectionString = Environment.GetEnvironmentVariable("DEVCONTROL_TEST_CONNECTION_STRING");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return;
        }

        await using var factory = new DevControlStage6Factory(connectionString);
        await factory.ResetDatabaseAsync();
        using var ownerClient = await factory.CreateAuthenticatedClientAsync("owner@example.com");
        var (_, organization, project, environment) = await CreateTenantAsync(ownerClient);

        var endpoint = await PostJsonAsync<WebhookEndpointCreateDto>(
            ownerClient,
            $"/api/organizations/{organization.Id}/projects/{project.Id}/environments/{environment.Id}/webhook-endpoints",
            new
            {
                name = "Receiver",
                url = "https://hooks.example.com/devcontrol",
                eventTypes = new[] { "webhook.test", "feature_flag.created" }
            });
        Assert.StartsWith("dwhsec_", endpoint.Secret, StringComparison.Ordinal);

        var listBody = await ownerClient.GetStringAsync($"/api/organizations/{organization.Id}/webhook-endpoints");
        Assert.DoesNotContain(endpoint.Secret, listBody, StringComparison.Ordinal);
        Assert.Contains(endpoint.SecretPrefix, listBody, StringComparison.Ordinal);

        factory.Outbound.Enqueue(new SafeOutboundResponse(SafeOutboundResultKind.Completed, HttpStatusCode.Accepted, "accepted", false, 8, null, TimeSpan.FromMilliseconds(5)));
        var testDelivery = await PostJsonAsync<WebhookDeliveryDto>(
            ownerClient,
            $"/api/organizations/{organization.Id}/webhook-endpoints/{endpoint.Id}/test-deliveries",
            new { });
        Assert.Equal("Succeeded", testDelivery.Status);
        Assert.Equal(1, testDelivery.AttemptCount);
        Assert.Equal(202, testDelivery.LastStatusCode);
        var request = Assert.Single(factory.Outbound.Requests);
        Assert.Equal("webhook.test", request.Headers["X-DevControl-Event"]);
        Assert.StartsWith("sha256=", request.Headers["X-DevControl-Signature"], StringComparison.Ordinal);

        _ = await PostJsonAsync<WebhookEndpointDto>(
            ownerClient,
            $"/api/organizations/{organization.Id}/webhook-endpoints/{endpoint.Id}/pause",
            new { });
        var skipped = await PostJsonAsync<WebhookDeliveryDto>(
            ownerClient,
            $"/api/organizations/{organization.Id}/webhook-endpoints/{endpoint.Id}/test-deliveries",
            new { });
        Assert.Equal("SkippedPaused", skipped.Status);
        Assert.Single(factory.Outbound.Requests);

        _ = await PostJsonAsync<WebhookEndpointDto>(
            ownerClient,
            $"/api/organizations/{organization.Id}/webhook-endpoints/{endpoint.Id}/resume",
            new { });
        factory.Outbound.Enqueue(new SafeOutboundResponse(SafeOutboundResultKind.Completed, HttpStatusCode.InternalServerError, "bad", false, 3, null, TimeSpan.FromMilliseconds(5)));
        var failed = await PostJsonAsync<WebhookDeliveryDto>(
            ownerClient,
            $"/api/organizations/{organization.Id}/webhook-endpoints/{endpoint.Id}/test-deliveries",
            new { });
        Assert.Equal("Failed", failed.Status);

        factory.Outbound.Enqueue(new SafeOutboundResponse(SafeOutboundResultKind.Completed, HttpStatusCode.OK, "ok", false, 2, null, TimeSpan.FromMilliseconds(5)));
        var retried = await PostJsonAsync<WebhookDeliveryDto>(
            ownerClient,
            $"/api/organizations/{organization.Id}/webhook-deliveries/{failed.Id}/retry",
            new { });
        Assert.Equal("Succeeded", retried.Status);
        Assert.Equal(2, retried.AttemptCount);

        factory.Outbound.Enqueue(new SafeOutboundResponse(SafeOutboundResultKind.Completed, HttpStatusCode.OK, "flag", false, 4, null, TimeSpan.FromMilliseconds(5)));
        _ = await PostJsonAsync<FeatureFlagDto>(
            ownerClient,
            $"/api/organizations/{organization.Id}/projects/{project.Id}/environments/{environment.Id}/feature-flags",
            new { key = "checkout.stage6", name = "Checkout Stage 6", kind = "FeatureFlag", enabled = true, reason = "Stage 6 proof" });
        using var schedulerRequest = new HttpRequestMessage(HttpMethod.Post, "/internal/scheduler/tick");
        schedulerRequest.Headers.Add("X-DevControl-Scheduler-Secret", DevControlStage6Factory.SchedulerSecret);
        var schedulerResponse = await ownerClient.SendAsync(schedulerRequest);
        schedulerResponse.EnsureSuccessStatusCode();

        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DevControlDbContext>();
        Assert.Contains(await dbContext.WebhookEvents.ToListAsync(), webhookEvent => webhookEvent.EventType == "feature_flag.created");
        Assert.Contains(await dbContext.WebhookDeliveryAttempts.ToListAsync(), attempt => attempt.Succeeded);
        Assert.Contains(await dbContext.AuditLogs.ToListAsync(), auditLog => auditLog.Action == "webhook_endpoint.create");
        Assert.Contains(await dbContext.ControlActions.ToListAsync(), action => action.ActionType == "webhook_endpoint.test");
    }

    [Fact]
    public async Task PrivateWebhookUrlsAreBlocked()
    {
        var connectionString = Environment.GetEnvironmentVariable("DEVCONTROL_TEST_CONNECTION_STRING");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return;
        }

        await using var factory = new DevControlStage6Factory(connectionString);
        await factory.ResetDatabaseAsync();
        using var ownerClient = await factory.CreateAuthenticatedClientAsync("owner@example.com");
        var (_, organization, project, environment) = await CreateTenantAsync(ownerClient);

        var response = await PostJsonRawAsync(
            ownerClient,
            $"/api/organizations/{organization.Id}/projects/{project.Id}/environments/{environment.Id}/webhook-endpoints",
            new
            {
                name = "Blocked",
                url = "https://127.0.0.1/hook",
                eventTypes = new[] { "webhook.test" }
            });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty(factory.Outbound.Requests);
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
            new { name = "Sample App", slug = "sample-app", description = "Stage 6 sample" });
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

    private sealed class DevControlStage6Factory : WebApplicationFactory<Program>
    {
        public const string SchedulerSecret = "test-scheduler-secret";
        private readonly string? originalConnectionString;
        private readonly string? originalSchedulerSecret;

        public DevControlStage6Factory(string connectionString)
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

    private sealed record WebhookEndpointDto(Guid Id, string Name, string Url, string SecretPrefix, IReadOnlyList<string> EventTypes, bool IsPaused);

    private sealed record WebhookEndpointCreateDto(Guid Id, string Name, string Url, string SecretPrefix, IReadOnlyList<string> EventTypes, bool IsPaused, string Secret);

    private sealed record WebhookDeliveryDto(Guid Id, Guid EndpointId, Guid EventId, string EventType, string Status, int AttemptCount, int? LastStatusCode);

    private sealed record FeatureFlagDto(Guid Id, string Key, bool Enabled);
}
