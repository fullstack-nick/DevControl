using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using DevControl.Application.Email;
using DevControl.Application.Security;
using DevControl.Domain.Entities;
using DevControl.Infrastructure.Database;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace DevControl.IntegrationTests;

[Collection(PostgresIntegrationCollection.Name)]
public sealed partial class FeatureFlagEndpointTests
{
    [Fact]
    public async Task ProductionChangesRequireAdminAndReason_AndWriteHistoryAuditAndControlActions()
    {
        var connectionString = Environment.GetEnvironmentVariable("DEVCONTROL_TEST_CONNECTION_STRING");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return;
        }

        await using var factory = new DevControlStage5Factory(connectionString);
        await factory.ResetDatabaseAsync();
        using var ownerClient = await factory.CreateAuthenticatedClientAsync("owner@example.com");
        var (_, organization, project, production) = await CreateTenantAsync(ownerClient, "Production", "production");
        var staging = await PostJsonAsync<EnvironmentDto>(
            ownerClient,
            $"/api/organizations/{organization.Id}/projects/{project.Id}/environments",
            new { name = "Staging", slug = "staging" });

        _ = await PostJsonAsync<InvitationDto>(
            ownerClient,
            $"/api/organizations/{organization.Id}/invitations",
            new { email = "developer@example.com", role = "Developer" });
        using var developerClient = await factory.CreateAuthenticatedClientAsync("developer@example.com");
        var accept = await PostJsonRawAsync(developerClient, $"/api/invitations/{factory.EmailSender.LastInvitationToken()}/accept", new { });
        Assert.Equal(HttpStatusCode.OK, accept.StatusCode);

        var stagingFlag = await CreateFeatureFlagAsync(
            developerClient,
            organization.Id,
            project.Id,
            staging.Id,
            "checkout.staging",
            "FeatureFlag",
            enabled: true);
        Assert.True(stagingFlag.Enabled);

        var denied = await PostJsonRawAsync(
            developerClient,
            $"/api/organizations/{organization.Id}/projects/{project.Id}/environments/{production.Id}/feature-flags",
            new { key = "checkout.prod.denied", name = "Denied", kind = "FeatureFlag", enabled = true, reason = "developer prod attempt" });
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);

        var missingReason = await PostJsonRawAsync(
            ownerClient,
            $"/api/organizations/{organization.Id}/projects/{project.Id}/environments/{production.Id}/feature-flags",
            new { key = "checkout.prod.missing_reason", name = "Missing reason", kind = "FeatureFlag", enabled = true });
        Assert.Equal(HttpStatusCode.BadRequest, missingReason.StatusCode);

        var prodFlag = await CreateFeatureFlagAsync(
            ownerClient,
            organization.Id,
            project.Id,
            production.Id,
            "checkout.prod",
            "FeatureFlag",
            enabled: true,
            "Initial production rollout.");

        var updated = await PatchJsonAsync<FeatureFlagDto>(
            ownerClient,
            $"/api/organizations/{organization.Id}/feature-flags/{prodFlag.Id}",
            new { enabled = false, reason = "Disable production rollout." });
        Assert.False(updated.Enabled);

        var changes = await ownerClient.GetFromJsonAsync<List<FeatureFlagChangeDto>>($"/api/organizations/{organization.Id}/feature-flags/{prodFlag.Id}/changes");
        Assert.NotNull(changes);
        Assert.True(changes!.Count >= 2);
        Assert.Contains(changes, change => change.Reason == "Disable production rollout.");

        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DevControlDbContext>();
        Assert.Contains(await dbContext.AuditLogs.ToListAsync(), auditLog => auditLog.Action == "feature_flag.create");
        Assert.Contains(await dbContext.AuditLogs.ToListAsync(), auditLog => auditLog.Action == "feature_flag.update");
        Assert.Contains(await dbContext.AuditLogs.ToListAsync(), auditLog => auditLog.Action == "feature_flag.create.denied");
        Assert.Contains(await dbContext.ControlActions.ToListAsync(), action => action.ActionType == "feature_flag.create");
        Assert.Contains(await dbContext.ControlActions.ToListAsync(), action => action.ActionType == "feature_flag.update");
        Assert.True(await dbContext.FeatureFlagChanges.CountAsync(change => change.FeatureFlagId == prodFlag.Id) >= 2);
    }

    [Fact]
    public async Task RuntimeSnapshotRequiresFlagsScopeUsesEtagAndIsScopedToApiKeyEnvironment()
    {
        var connectionString = Environment.GetEnvironmentVariable("DEVCONTROL_TEST_CONNECTION_STRING");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return;
        }

        await using var factory = new DevControlStage5Factory(connectionString);
        await factory.ResetDatabaseAsync();
        using var ownerClient = await factory.CreateAuthenticatedClientAsync("owner@example.com");
        var (_, organization, project, production) = await CreateTenantAsync(ownerClient, "Production", "production");
        var staging = await PostJsonAsync<EnvironmentDto>(
            ownerClient,
            $"/api/organizations/{organization.Id}/projects/{project.Id}/environments",
            new { name = "Staging", slug = "staging" });

        _ = await CreateFeatureFlagAsync(ownerClient, organization.Id, project.Id, production.Id, "checkout.enabled", "FeatureFlag", true, "Enable checkout.");
        _ = await CreateFeatureFlagAsync(ownerClient, organization.Id, project.Id, production.Id, "checkout.kill", "KillSwitch", false, "Allow checkout.");
        _ = await CreateFeatureFlagAsync(ownerClient, organization.Id, project.Id, staging.Id, "staging.only", "FeatureFlag", true);

        var flagsKey = await CreateApiKeyAsync(ownerClient, organization.Id, project.Id, production.Id, ["flags:read"], 10);
        var sampleOnlyKey = await CreateApiKeyAsync(ownerClient, organization.Id, project.Id, production.Id, ["sample:read"], 10);
        var stagingKey = await CreateApiKeyAsync(ownerClient, organization.Id, project.Id, staging.Id, ["flags:read"], 10);
        using var runtimeClient = factory.CreateClient();

        var forbidden = await SendRuntimeSnapshotAsync(runtimeClient, sampleOnlyKey.Secret);
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);

        var snapshotResponse = await SendRuntimeSnapshotAsync(runtimeClient, flagsKey.Secret);
        snapshotResponse.EnsureSuccessStatusCode();
        var etag = snapshotResponse.Headers.ETag?.ToString();
        Assert.False(string.IsNullOrWhiteSpace(etag));
        var snapshot = await snapshotResponse.Content.ReadFromJsonAsync<SnapshotDto>();
        Assert.NotNull(snapshot);
        Assert.True(snapshot!.Flags["checkout.enabled"]);
        Assert.False(snapshot.KillSwitches["checkout.kill"]);
        Assert.DoesNotContain("staging.only", snapshot.Flags.Keys);

        var notModified = await SendRuntimeSnapshotAsync(runtimeClient, flagsKey.Secret, etag);
        Assert.Equal(HttpStatusCode.NotModified, notModified.StatusCode);

        var stagingSnapshotResponse = await SendRuntimeSnapshotAsync(runtimeClient, stagingKey.Secret);
        stagingSnapshotResponse.EnsureSuccessStatusCode();
        var stagingSnapshot = await stagingSnapshotResponse.Content.ReadFromJsonAsync<SnapshotDto>();
        Assert.NotNull(stagingSnapshot);
        Assert.True(stagingSnapshot!.Flags["staging.only"]);
        Assert.DoesNotContain("checkout.enabled", stagingSnapshot.Flags.Keys);

        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DevControlDbContext>();
        var meteredKey = await dbContext.ApiKeys.SingleAsync(candidate => candidate.Id == flagsKey.Id);
        Assert.Equal(2, meteredKey.TotalRequestCount);
        Assert.Equal(0, meteredKey.FailureCount);
        Assert.NotNull(meteredKey.LastUsedAt);
    }

    private static async Task<FeatureFlagDto> CreateFeatureFlagAsync(
        HttpClient client,
        Guid organizationId,
        Guid projectId,
        Guid environmentId,
        string key,
        string kind,
        bool enabled,
        string? reason = null)
    {
        return await PostJsonAsync<FeatureFlagDto>(
            client,
            $"/api/organizations/{organizationId}/projects/{projectId}/environments/{environmentId}/feature-flags",
            new { key, name = key, kind, enabled, reason });
    }

    private static async Task<ApiKeyCreateDto> CreateApiKeyAsync(
        HttpClient client,
        Guid organizationId,
        Guid projectId,
        Guid environmentId,
        string[] scopes,
        int rateLimitPerMinute)
    {
        return await PostJsonAsync<ApiKeyCreateDto>(
            client,
            $"/api/organizations/{organizationId}/projects/{projectId}/environments/{environmentId}/api-keys",
            new { name = $"{string.Join("-", scopes)} key", scopes, rateLimitPerMinute });
    }

    private static async Task<HttpResponseMessage> SendRuntimeSnapshotAsync(HttpClient client, string apiKey, string? etag = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/runtime/flags/snapshot");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        if (!string.IsNullOrWhiteSpace(etag))
        {
            request.Headers.TryAddWithoutValidation("If-None-Match", etag);
        }

        return await client.SendAsync(request);
    }

    private static async Task<(MeDto Me, OrganizationDto Organization, ProjectDto Project, EnvironmentDto Environment)> CreateTenantAsync(
        HttpClient client,
        string environmentName,
        string environmentSlug)
    {
        var organization = await PostJsonAsync<OrganizationDto>(
            client,
            "/api/organizations",
            new { name = "Acme Platform", slug = "acme-platform" });
        var project = await PostJsonAsync<ProjectDto>(
            client,
            $"/api/organizations/{organization.Id}/projects",
            new { name = "Sample App", slug = "sample-app", description = "Stage 5 sample" });
        var environment = await PostJsonAsync<EnvironmentDto>(
            client,
            $"/api/organizations/{organization.Id}/projects/{project.Id}/environments",
            new { name = environmentName, slug = environmentSlug });
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

    private static async Task<T> PatchJsonAsync<T>(HttpClient client, string path, object payload)
    {
        var csrf = await client.GetFromJsonAsync<CsrfDto>("/api/auth/csrf");
        using var request = new HttpRequestMessage(HttpMethod.Patch, path)
        {
            Content = JsonContent.Create(payload)
        };
        request.Headers.Add("X-CSRF-TOKEN", csrf?.Token ?? throw new InvalidOperationException("Missing CSRF token."));
        var response = await client.SendAsync(request);
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

    private sealed class DevControlStage5Factory : WebApplicationFactory<Program>
    {
        private readonly string? originalConnectionString;

        public DevControlStage5Factory(string connectionString)
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

    private sealed record FeatureFlagDto(
        Guid Id,
        string Key,
        string Name,
        string Description,
        string Kind,
        bool Enabled,
        Guid ProjectId,
        Guid EnvironmentId);

    private sealed record FeatureFlagChangeDto(
        Guid Id,
        Guid FeatureFlagId,
        bool OldValue,
        bool NewValue,
        string Reason,
        string ChangedByEmail,
        DateTimeOffset ChangedAt);

    private sealed record ApiKeyCreateDto(
        Guid Id,
        string Name,
        string KeyPrefix,
        IReadOnlyList<string> Scopes,
        int RateLimitPerMinute,
        Guid ProjectId,
        Guid EnvironmentId,
        DateTimeOffset? RevokedAt,
        Guid? RotatedFromApiKeyId,
        string Secret);

    private sealed record SnapshotDto(
        string Version,
        DateTimeOffset GeneratedAt,
        int RefreshIntervalSeconds,
        int KillSwitchRefreshIntervalSeconds,
        Dictionary<string, bool> Flags,
        Dictionary<string, bool> KillSwitches);
}
