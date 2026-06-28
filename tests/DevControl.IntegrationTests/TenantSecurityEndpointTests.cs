using System.Collections.Concurrent;
using System.Net;
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
public sealed partial class TenantSecurityEndpointTests
{
    [Fact]
    public async Task OpenSignup_CreatesTenantResources_AndWritesAuditLogs()
    {
        var connectionString = Environment.GetEnvironmentVariable("DEVCONTROL_TEST_CONNECTION_STRING");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return;
        }

        await using var factory = new DevControlStage2Factory(connectionString);
        await factory.ResetDatabaseAsync();
        using var client = await factory.CreateAuthenticatedClientAsync("owner@example.com");

        var me = await client.GetFromJsonAsync<MeDto>("/api/auth/me");
        Assert.NotNull(me);
        Assert.Empty(me.Organizations);

        var organization = await CreateOrganizationAsync(client, "Acme Platform");
        var project = await PostJsonAsync<ProjectDto>(
            client,
            $"/api/organizations/{organization.Id}/projects",
            new { name = "Control Plane", slug = "control-plane", description = "Stage 2 test project" });
        _ = await PostJsonAsync<EnvironmentDto>(
            client,
            $"/api/organizations/{organization.Id}/projects/{project.Id}/environments",
            new { name = "Production", slug = "production" });

        var auditLogs = await client.GetFromJsonAsync<List<AuditDto>>($"/api/organizations/{organization.Id}/audit-logs");

        Assert.Contains(auditLogs!, auditLog => auditLog.Action == "organization.create");
        Assert.Contains(auditLogs!, auditLog => auditLog.Action == "project.create");
        Assert.Contains(auditLogs!, auditLog => auditLog.Action == "environment.create");
    }

    [Fact]
    public async Task Rbac_InvitationAccept_AndTenantScoping_AreEnforced()
    {
        var connectionString = Environment.GetEnvironmentVariable("DEVCONTROL_TEST_CONNECTION_STRING");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return;
        }

        await using var factory = new DevControlStage2Factory(connectionString);
        await factory.ResetDatabaseAsync();
        using var ownerClient = await factory.CreateAuthenticatedClientAsync("owner@example.com");
        var organization = await CreateOrganizationAsync(ownerClient, "Secure Org");

        _ = await PostJsonAsync<InvitationDto>(
            ownerClient,
            $"/api/organizations/{organization.Id}/invitations",
            new { email = "viewer@example.com", role = "Viewer" });
        var token = factory.EmailSender.LastInvitationToken();

        using var viewerClient = await factory.CreateAuthenticatedClientAsync("viewer@example.com");
        var acceptResponse = await PostJsonRawAsync(viewerClient, $"/api/invitations/{token}/accept", new { });
        Assert.Equal(HttpStatusCode.OK, acceptResponse.StatusCode);

        var forbiddenProjectResponse = await PostJsonRawAsync(
            viewerClient,
            $"/api/organizations/{organization.Id}/projects",
            new { name = "Denied", slug = "denied", description = "" });
        Assert.Equal(HttpStatusCode.Forbidden, forbiddenProjectResponse.StatusCode);

        using var outsiderClient = await factory.CreateAuthenticatedClientAsync("outsider@example.com");
        var outsiderOrganization = await CreateOrganizationAsync(outsiderClient, "Outsider Org");
        var hiddenResponse = await ownerClient.GetAsync($"/api/organizations/{outsiderOrganization.Id}");
        Assert.Equal(HttpStatusCode.NotFound, hiddenResponse.StatusCode);

        var auditLogs = await ownerClient.GetFromJsonAsync<List<AuditDto>>($"/api/organizations/{organization.Id}/audit-logs");
        Assert.Contains(auditLogs!, auditLog => auditLog.Action == "invitation.accept");
        Assert.Contains(auditLogs!, auditLog => auditLog.Action == "project.create.denied" && auditLog.Outcome == "Denied");
    }

    [Fact]
    public async Task Invitations_CanBeResentRevoked_AndRejectMismatchedEmail()
    {
        var connectionString = Environment.GetEnvironmentVariable("DEVCONTROL_TEST_CONNECTION_STRING");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return;
        }

        await using var factory = new DevControlStage2Factory(connectionString);
        await factory.ResetDatabaseAsync();
        using var ownerClient = await factory.CreateAuthenticatedClientAsync("owner@example.com");
        var organization = await CreateOrganizationAsync(ownerClient, "Invite Org");

        var invitation = await PostJsonAsync<InvitationDto>(
            ownerClient,
            $"/api/organizations/{organization.Id}/invitations",
            new { email = "developer@example.com", role = "Developer" });
        var firstToken = factory.EmailSender.LastInvitationToken();

        using var wrongClient = await factory.CreateAuthenticatedClientAsync("wrong@example.com");
        var wrongAccept = await PostJsonRawAsync(wrongClient, $"/api/invitations/{firstToken}/accept", new { });
        Assert.Equal(HttpStatusCode.Forbidden, wrongAccept.StatusCode);

        _ = await PostJsonAsync<InvitationDto>(
            ownerClient,
            $"/api/organizations/{organization.Id}/invitations/{invitation.Id}/resend",
            new { });
        Assert.True(factory.EmailSender.Messages.Count >= 2);

        var revoked = await PostJsonAsync<InvitationDto>(
            ownerClient,
            $"/api/organizations/{organization.Id}/invitations/{invitation.Id}/revoke",
            new { });
        Assert.Equal("Revoked", revoked.Status);
    }

    [Fact]
    public async Task LastOwner_CannotBeRemoved()
    {
        var connectionString = Environment.GetEnvironmentVariable("DEVCONTROL_TEST_CONNECTION_STRING");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return;
        }

        await using var factory = new DevControlStage2Factory(connectionString);
        await factory.ResetDatabaseAsync();
        using var ownerClient = await factory.CreateAuthenticatedClientAsync("owner@example.com");
        var organization = await CreateOrganizationAsync(ownerClient, "Owner Guard Org");
        var members = await ownerClient.GetFromJsonAsync<List<MemberDto>>($"/api/organizations/{organization.Id}/members");

        var removeResponse = await DeleteAsync(ownerClient, $"/api/organizations/{organization.Id}/members/{members![0].Id}");

        Assert.Equal(HttpStatusCode.BadRequest, removeResponse.StatusCode);
    }

    private static async Task<OrganizationDto> CreateOrganizationAsync(HttpClient client, string name)
    {
        return await PostJsonAsync<OrganizationDto>(
            client,
            "/api/organizations",
            new { name, slug = "" });
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

    private static async Task<HttpResponseMessage> DeleteAsync(HttpClient client, string path)
    {
        var csrf = await client.GetFromJsonAsync<CsrfDto>("/api/auth/csrf");
        using var request = new HttpRequestMessage(HttpMethod.Delete, path);
        request.Headers.Add("X-CSRF-TOKEN", csrf?.Token ?? throw new InvalidOperationException("Missing CSRF token."));
        return await client.SendAsync(request);
    }

    private sealed class DevControlStage2Factory : WebApplicationFactory<Program>
    {
        private readonly string? originalConnectionString;

        public DevControlStage2Factory(string connectionString)
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

    private sealed record MemberDto(Guid Id, Guid UserId, string Email, string DisplayName, string Role);

    private sealed record AuditDto(Guid Id, string ActorEmail, string Action, string Outcome, string TargetType, string Message);
}
