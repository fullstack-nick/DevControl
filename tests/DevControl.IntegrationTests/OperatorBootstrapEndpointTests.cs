using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using DevControl.Application.Security;
using DevControl.Infrastructure.Database;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace DevControl.IntegrationTests;

[Collection(PostgresIntegrationCollection.Name)]
public sealed class OperatorBootstrapEndpointTests
{
    private const string OperatorSecret = "operator-secret-for-tests";

    [Fact]
    public async Task BootstrapLiveProof_IsDisabledWithoutConfiguredSecret()
    {
        var connectionString = Environment.GetEnvironmentVariable("DEVCONTROL_TEST_CONNECTION_STRING");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return;
        }

        await using var factory = new OperatorBootstrapFactory(connectionString, null);
        await factory.ResetDatabaseAsync();
        using var client = factory.CreateClient();

        var response = await PostBootstrapRawAsync(client, OperatorSecret, new { ownerEmail = "owner@example.com" });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task BootstrapLiveProof_RepairsTenant_RotatesProofSecrets_AndSupportsLiveProofCalls()
    {
        var connectionString = Environment.GetEnvironmentVariable("DEVCONTROL_TEST_CONNECTION_STRING");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return;
        }

        await using var factory = new OperatorBootstrapFactory(connectionString, OperatorSecret);
        await factory.ResetDatabaseAsync();
        using var client = factory.CreateClient();

        Assert.Equal(
            HttpStatusCode.Unauthorized,
            (await PostBootstrapRawAsync(client, "wrong-secret", new { ownerEmail = "owner@example.com" })).StatusCode);

        var first = await PostBootstrapAsync(client, OperatorSecret, new { ownerEmail = "owner@example.com", ownerName = "Owner User" });
        Assert.StartsWith("dcr_", first.RegistrationToken.Secret, StringComparison.Ordinal);
        Assert.StartsWith("dck_", first.ApiKey.Secret, StringComparison.Ordinal);

        var second = await PostBootstrapAsync(client, OperatorSecret, new { ownerEmail = "owner@example.com", ownerName = "Owner User" });
        Assert.Equal(first.Organization.Id, second.Organization.Id);
        Assert.Equal(first.Project.Id, second.Project.Id);
        Assert.Equal(first.Environment.Id, second.Environment.Id);
        Assert.NotEqual(first.RegistrationToken.Secret, second.RegistrationToken.Secret);
        Assert.NotEqual(first.ApiKey.Secret, second.ApiKey.Secret);
        Assert.Contains(first.RegistrationToken.Id, second.RevokedRegistrationTokenIds);
        Assert.Contains(first.ApiKey.Id, second.RevokedApiKeyIds);

        using var ownerClient = await factory.CreateAuthenticatedClientAsync("owner@example.com");
        var tokenList = await ownerClient.GetStringAsync($"/api/organizations/{second.Organization.Id}/registration-tokens");
        var apiKeyList = await ownerClient.GetStringAsync($"/api/organizations/{second.Organization.Id}/api-keys");
        Assert.DoesNotContain(first.RegistrationToken.Secret, tokenList, StringComparison.Ordinal);
        Assert.DoesNotContain(second.RegistrationToken.Secret, tokenList, StringComparison.Ordinal);
        Assert.DoesNotContain(first.ApiKey.Secret, apiKeyList, StringComparison.Ordinal);
        Assert.DoesNotContain(second.ApiKey.Secret, apiKeyList, StringComparison.Ordinal);

        var registerResponse = await RegisterAsync(client, second.RegistrationToken.Secret, RegistrationPayload("v1", second.Environment.Slug));
        Assert.Equal(HttpStatusCode.OK, registerResponse.StatusCode);

        using var runtimeRequest = new HttpRequestMessage(HttpMethod.Get, "/api/runtime/sample/echo?delayMs=1");
        runtimeRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", second.ApiKey.Secret);
        Assert.Equal(HttpStatusCode.OK, (await client.SendAsync(runtimeRequest)).StatusCode);

        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DevControlDbContext>();
        Assert.Equal(1, await dbContext.Organizations.CountAsync());
        Assert.Equal(1, await dbContext.Projects.CountAsync());
        Assert.Equal(1, await dbContext.ProjectEnvironments.CountAsync());
        Assert.Contains(await dbContext.AuditLogs.ToListAsync(), auditLog => auditLog.Action == "operator.bootstrap.live_proof");
        Assert.Contains(await dbContext.ControlActions.ToListAsync(), action => action.ActionType == "operator.bootstrap.live_proof");
        Assert.NotNull((await dbContext.RegistrationTokens.SingleAsync(token => token.Id == first.RegistrationToken.Id)).RevokedAt);
        Assert.NotNull((await dbContext.ApiKeys.SingleAsync(apiKey => apiKey.Id == first.ApiKey.Id)).RevokedAt);
        Assert.NotNull((await dbContext.ApiKeys.SingleAsync(apiKey => apiKey.Id == second.ApiKey.Id)).LastUsedAt);

        var persistedParts = (await dbContext.AuditLogs.Select(auditLog => auditLog.MetadataJson).ToListAsync())
            .Concat(await dbContext.ControlActions.Select(controlAction => controlAction.RequestJson + controlAction.ResultJson).ToListAsync());
        var persistedJson = string.Join("\n", persistedParts);
        Assert.DoesNotContain(first.RegistrationToken.Secret, persistedJson, StringComparison.Ordinal);
        Assert.DoesNotContain(second.RegistrationToken.Secret, persistedJson, StringComparison.Ordinal);
        Assert.DoesNotContain(first.ApiKey.Secret, persistedJson, StringComparison.Ordinal);
        Assert.DoesNotContain(second.ApiKey.Secret, persistedJson, StringComparison.Ordinal);
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

    private static async Task<OperatorBootstrapDto> PostBootstrapAsync(HttpClient client, string secret, object payload)
    {
        var response = await PostBootstrapRawAsync(client, secret, payload);
        response.EnsureSuccessStatusCode();
        var value = await response.Content.ReadFromJsonAsync<OperatorBootstrapDto>();
        return value ?? throw new InvalidOperationException("Bootstrap response was empty.");
    }

    private static async Task<HttpResponseMessage> PostBootstrapRawAsync(HttpClient client, string? secret, object payload)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/operator/bootstrap-live-proof")
        {
            Content = JsonContent.Create(payload)
        };
        if (!string.IsNullOrWhiteSpace(secret))
        {
            request.Headers.Add(OperatorSecretValidator.HeaderName, secret);
        }

        return await client.SendAsync(request);
    }

    private sealed class OperatorBootstrapFactory : WebApplicationFactory<Program>
    {
        private readonly string? originalConnectionString;
        private readonly string? originalOperatorSecret;

        public OperatorBootstrapFactory(string connectionString, string? operatorSecret)
        {
            originalConnectionString = Environment.GetEnvironmentVariable("ConnectionStrings__DevControl");
            originalOperatorSecret = Environment.GetEnvironmentVariable("DEVCONTROL_OPERATOR_BOOTSTRAP_SECRET");
            Environment.SetEnvironmentVariable("ConnectionStrings__DevControl", connectionString);
            Environment.SetEnvironmentVariable("DEVCONTROL_OPERATOR_BOOTSTRAP_SECRET", operatorSecret);
        }

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
        }

        protected override void Dispose(bool disposing)
        {
            Environment.SetEnvironmentVariable("ConnectionStrings__DevControl", originalConnectionString);
            Environment.SetEnvironmentVariable("DEVCONTROL_OPERATOR_BOOTSTRAP_SECRET", originalOperatorSecret);
            base.Dispose(disposing);
        }
    }

    private sealed record OperatorBootstrapDto(
        UserDto Owner,
        OrganizationDto Organization,
        ProjectDto Project,
        EnvironmentDto Environment,
        RegistrationTokenDto RegistrationToken,
        ApiKeyDto ApiKey,
        IReadOnlyList<Guid> RevokedRegistrationTokenIds,
        IReadOnlyList<Guid> RevokedApiKeyIds);

    private sealed record UserDto(Guid Id, string Email, string DisplayName);

    private sealed record OrganizationDto(Guid Id, string Name, string Slug);

    private sealed record ProjectDto(Guid Id, string Name, string Slug);

    private sealed record EnvironmentDto(Guid Id, string Name, string Slug);

    private sealed record RegistrationTokenDto(Guid Id, string Name, string TokenPrefix, string Scope, string Secret);

    private sealed record ApiKeyDto(Guid Id, string Name, string KeyPrefix, IReadOnlyList<string> Scopes, int RateLimitPerMinute, string Secret);
}
