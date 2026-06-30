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
public sealed partial class ApiKeyEndpointTests
{
    [Fact]
    public async Task AdminCanCreateRevokeRotateKeys_DeveloperCannotMutate_AndSecretsAreShownOnce()
    {
        var connectionString = Environment.GetEnvironmentVariable("DEVCONTROL_TEST_CONNECTION_STRING");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return;
        }

        await using var factory = new DevControlStage4Factory(connectionString);
        await factory.ResetDatabaseAsync();
        using var ownerClient = await factory.CreateAuthenticatedClientAsync("owner@example.com");
        var (_, organization, project, environment) = await CreateTenantAsync(ownerClient);

        var apiKey = await CreateApiKeyAsync(ownerClient, organization.Id, project.Id, environment.Id, "Runtime demo", 10);
        Assert.StartsWith("dck_", apiKey.Secret, StringComparison.Ordinal);
        Assert.Equal("sample:read", Assert.Single(apiKey.Scopes));

        var listResponse = await ownerClient.GetAsync($"/api/organizations/{organization.Id}/api-keys");
        listResponse.EnsureSuccessStatusCode();
        var listBody = await listResponse.Content.ReadAsStringAsync();
        Assert.DoesNotContain(apiKey.Secret, listBody, StringComparison.Ordinal);
        Assert.Contains(apiKey.KeyPrefix, listBody, StringComparison.Ordinal);

        _ = await PostJsonAsync<InvitationDto>(
            ownerClient,
            $"/api/organizations/{organization.Id}/invitations",
            new { email = "developer@example.com", role = "Developer" });
        using var developerClient = await factory.CreateAuthenticatedClientAsync("developer@example.com");
        var accept = await PostJsonRawAsync(developerClient, $"/api/invitations/{factory.EmailSender.LastInvitationToken()}/accept", new { });
        Assert.Equal(HttpStatusCode.OK, accept.StatusCode);

        var denied = await PostJsonRawAsync(
            developerClient,
            $"/api/organizations/{organization.Id}/projects/{project.Id}/environments/{environment.Id}/api-keys",
            new { name = "Denied", scopes = new[] { "sample:read" } });
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);

        var revoke = await PostJsonAsync<ApiKeyRevokeDto>(
            ownerClient,
            $"/api/organizations/{organization.Id}/api-keys/{apiKey.Id}/revoke",
            new { });
        Assert.NotNull(revoke.RevokedAt);

        var activeForRotation = await CreateApiKeyAsync(ownerClient, organization.Id, project.Id, environment.Id, "Rotate me", 10);
        var rotated = await PostJsonAsync<ApiKeyCreateDto>(
            ownerClient,
            $"/api/organizations/{organization.Id}/api-keys/{activeForRotation.Id}/rotate",
            new { });

        Assert.StartsWith("dck_", rotated.Secret, StringComparison.Ordinal);
        Assert.NotEqual(activeForRotation.Secret, rotated.Secret);
        Assert.Equal(activeForRotation.Id, rotated.RotatedFromApiKeyId);

        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DevControlDbContext>();
        Assert.Contains(await dbContext.ControlActions.ToListAsync(), action => action.ActionType == "api_key.revoke");
        Assert.Contains(await dbContext.ControlActions.ToListAsync(), action => action.ActionType == "api_key.rotate");
        Assert.Contains(await dbContext.AuditLogs.ToListAsync(), auditLog => auditLog.Action == "api_key.create");
    }

    [Fact]
    public async Task RuntimeEndpoint_MetersSuccessFailureWrongScopeRevocationAndRateLimit()
    {
        var connectionString = Environment.GetEnvironmentVariable("DEVCONTROL_TEST_CONNECTION_STRING");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return;
        }

        await using var factory = new DevControlStage4Factory(connectionString);
        await factory.ResetDatabaseAsync();
        using var ownerClient = await factory.CreateAuthenticatedClientAsync("owner@example.com");
        var (_, organization, project, environment) = await CreateTenantAsync(ownerClient);
        var apiKey = await CreateApiKeyAsync(ownerClient, organization.Id, project.Id, environment.Id, "Metered", 2);

        using var runtimeClient = factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await runtimeClient.GetAsync("/api/runtime/sample/echo")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await SendRuntimeAsync(runtimeClient, "dck_bad", "/api/runtime/sample/echo")).StatusCode);

        Assert.Equal(HttpStatusCode.OK, (await SendRuntimeAsync(runtimeClient, apiKey.Secret, "/api/runtime/sample/echo?delayMs=10")).StatusCode);
        Assert.Equal(HttpStatusCode.InternalServerError, (await SendRuntimeAsync(runtimeClient, apiKey.Secret, "/api/runtime/sample/echo?status=500")).StatusCode);
        Assert.Equal((HttpStatusCode)429, (await SendRuntimeAsync(runtimeClient, apiKey.Secret, "/api/runtime/sample/echo")).StatusCode);

        var wrongScopeSecret = await CreateWrongScopeKeyAsync(factory, organization.Id, project.Id, environment.Id);
        Assert.Equal(HttpStatusCode.Forbidden, (await SendRuntimeAsync(runtimeClient, wrongScopeSecret, "/api/runtime/sample/echo")).StatusCode);

        await PostJsonAsync<ApiKeyRevokeDto>(
            ownerClient,
            $"/api/organizations/{organization.Id}/api-keys/{apiKey.Id}/revoke",
            new { });
        Assert.Equal(HttpStatusCode.Unauthorized, (await SendRuntimeAsync(runtimeClient, apiKey.Secret, "/api/runtime/sample/echo")).StatusCode);

        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DevControlDbContext>();
        var meteredKey = await dbContext.ApiKeys.SingleAsync(candidate => candidate.Id == apiKey.Id);
        Assert.Equal(4, meteredKey.TotalRequestCount);
        Assert.Equal(3, meteredKey.FailureCount);
        Assert.Equal(1, meteredKey.RateLimitHitCount);
        Assert.NotNull(meteredKey.LastUsedAt);
        Assert.True(meteredKey.LatencySampleCount >= 2);

        var daily = await dbContext.ApiKeyUsageDaily.SingleAsync(candidate => candidate.ApiKeyId == apiKey.Id);
        Assert.Equal(4, daily.RequestCount);
        Assert.Equal(3, daily.FailureCount);
        Assert.Equal(1, daily.RateLimitHitCount);

        var wrongScopePrefix = wrongScopeSecret[..16];
        var wrongScopeKey = await dbContext.ApiKeys.SingleAsync(candidate => candidate.KeyPrefix == wrongScopePrefix);
        Assert.Equal(1, wrongScopeKey.TotalRequestCount);
        Assert.Equal(1, wrongScopeKey.FailureCount);
    }

    private static async Task<ApiKeyCreateDto> CreateApiKeyAsync(
        HttpClient client,
        Guid organizationId,
        Guid projectId,
        Guid environmentId,
        string name,
        int rateLimitPerMinute)
    {
        return await PostJsonAsync<ApiKeyCreateDto>(
            client,
            $"/api/organizations/{organizationId}/projects/{projectId}/environments/{environmentId}/api-keys",
            new { name, scopes = new[] { "sample:read" }, rateLimitPerMinute });
    }

    private static async Task<string> CreateWrongScopeKeyAsync(
        DevControlStage4Factory factory,
        Guid organizationId,
        Guid projectId,
        Guid environmentId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DevControlDbContext>();
        var apiKeySecretService = scope.ServiceProvider.GetRequiredService<ApiKeySecretService>();
        var now = DateTimeOffset.UtcNow;
        var secret = apiKeySecretService.CreateKey();
        var owner = await dbContext.Users.SingleAsync(user => user.NormalizedEmail == "owner@example.com");
        dbContext.ApiKeys.Add(new ApiKey(
            organizationId,
            projectId,
            environmentId,
            "Wrong scope",
            secret.Prefix,
            secret.Hash,
            "[\"flags:read\"]",
            10,
            owner.Id,
            now));
        await dbContext.SaveChangesAsync();
        return secret.Secret;
    }

    private static async Task<HttpResponseMessage> SendRuntimeAsync(HttpClient client, string apiKey, string path)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
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
            new { name = "Sample App", slug = "sample-app", description = "Stage 4 sample" });
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

    private sealed class DevControlStage4Factory : WebApplicationFactory<Program>
    {
        private readonly string? originalConnectionString;

        public DevControlStage4Factory(string connectionString)
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

    private sealed record ApiKeyRevokeDto(Guid Id, DateTimeOffset? RevokedAt);
}
