using System.Net;
using DevControl.Domain.Entities;
using DevControl.Domain.Enums;
using DevControl.Infrastructure.Database;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace DevControl.IntegrationTests;

[Collection(PostgresIntegrationCollection.Name)]
public sealed class Stage9ObservabilityTests
{
    [Fact]
    public async Task MetricsEndpoint_IsDisabledByDefault()
    {
        await using var factory = new Stage9Factory("Host=127.0.0.1;Port=65432;Database=missing;Username=missing;Password=missing", metricsEnabled: false);
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/metrics");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task MetricsEndpoint_ReturnsPrometheusText_WhenEnabled()
    {
        await using var factory = new Stage9Factory("Host=127.0.0.1;Port=65432;Database=missing;Username=missing;Password=missing", metricsEnabled: true);
        using var client = factory.CreateClient();

        _ = await client.GetAsync("/health/live");
        var response = await client.GetAsync("/metrics");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("text/plain", response.Content.Headers.ContentType?.MediaType);
        Assert.Contains("devcontrol_http_requests_total", body);
    }

    [Fact]
    public async Task SchedulerTick_PrunesOnlyOldEphemeralRows_AndReturnsCleanupResult()
    {
        var connectionString = Environment.GetEnvironmentVariable("DEVCONTROL_TEST_CONNECTION_STRING");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return;
        }

        await using var factory = new Stage9Factory(connectionString, metricsEnabled: false);
        await factory.ResetDatabaseAsync();
        var seeded = await factory.SeedRetentionRowsAsync();
        using var client = factory.CreateClient();

        using var request = new HttpRequestMessage(HttpMethod.Post, "/internal/scheduler/tick");
        request.Headers.Add("X-DevControl-Scheduler-Secret", Stage9Factory.SchedulerSecret);
        var response = await client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();

        response.EnsureSuccessStatusCode();
        Assert.Contains("\"cleanup\"", body);
        Assert.Contains("\"apiKeyRateLimitWindowsDeleted\":1", body);
        Assert.Contains("\"monitorChecksDeleted\":1", body);

        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DevControlDbContext>();
        Assert.False(await dbContext.ApiKeyRateLimitWindows.AnyAsync(window => window.Id == seeded.OldRateLimitWindowId));
        Assert.True(await dbContext.ApiKeyRateLimitWindows.AnyAsync(window => window.Id == seeded.RecentRateLimitWindowId));
        Assert.False(await dbContext.MonitorChecks.AnyAsync(check => check.Id == seeded.OldMonitorCheckId));
        Assert.True(await dbContext.MonitorChecks.AnyAsync(check => check.Id == seeded.RecentMonitorCheckId));
        Assert.False(await dbContext.WebhookDeliveries.AnyAsync(delivery => delivery.Id == seeded.OldWebhookDeliveryId));
        Assert.True(await dbContext.WebhookDeliveries.AnyAsync(delivery => delivery.Id == seeded.RecentWebhookDeliveryId));
        Assert.True(await dbContext.AuditLogs.AnyAsync(auditLog => auditLog.Id == seeded.AuditLogId));
        Assert.True(await dbContext.ControlActions.AnyAsync(controlAction => controlAction.Id == seeded.ControlActionId));
        Assert.True(await dbContext.FeatureFlagChanges.AnyAsync(change => change.Id == seeded.FeatureFlagChangeId));
        Assert.True(await dbContext.Incidents.AnyAsync(incident => incident.Id == seeded.IncidentId));
        Assert.True(await dbContext.StatusReleases.AnyAsync(release => release.Id == seeded.ReleaseId));
        Assert.True(await dbContext.LiveAppDeployments.AnyAsync(deployment => deployment.Id == seeded.DeploymentId));
    }

    private sealed class Stage9Factory : WebApplicationFactory<Program>
    {
        public const string SchedulerSecret = "test-scheduler-secret";
        private readonly string? originalConnectionString;
        private readonly string? originalSchedulerSecret;
        private readonly string? originalMetricsEnabled;
        private readonly string? originalRateLimitDays;
        private readonly string? originalMonitorDays;
        private readonly string? originalWebhookPreviewDays;
        private readonly string? originalWebhookDeliveryDays;
        private readonly string? originalCleanupBatchSize;

        public Stage9Factory(string connectionString, bool metricsEnabled)
        {
            originalConnectionString = Environment.GetEnvironmentVariable("ConnectionStrings__DevControl");
            originalSchedulerSecret = Environment.GetEnvironmentVariable("DEVCONTROL_SCHEDULER_SECRET");
            originalMetricsEnabled = Environment.GetEnvironmentVariable("DEVCONTROL_METRICS_ENABLED");
            originalRateLimitDays = Environment.GetEnvironmentVariable("DEVCONTROL_RETENTION_RATE_LIMIT_WINDOWS_DAYS");
            originalMonitorDays = Environment.GetEnvironmentVariable("DEVCONTROL_RETENTION_MONITOR_CHECKS_DAYS");
            originalWebhookPreviewDays = Environment.GetEnvironmentVariable("DEVCONTROL_RETENTION_WEBHOOK_PREVIEW_DAYS");
            originalWebhookDeliveryDays = Environment.GetEnvironmentVariable("DEVCONTROL_RETENTION_WEBHOOK_DELIVERIES_DAYS");
            originalCleanupBatchSize = Environment.GetEnvironmentVariable("DEVCONTROL_CLEANUP_BATCH_SIZE");

            Environment.SetEnvironmentVariable("ConnectionStrings__DevControl", connectionString);
            Environment.SetEnvironmentVariable("DEVCONTROL_SCHEDULER_SECRET", SchedulerSecret);
            Environment.SetEnvironmentVariable("DEVCONTROL_METRICS_ENABLED", metricsEnabled ? "true" : "false");
            Environment.SetEnvironmentVariable("DEVCONTROL_RETENTION_RATE_LIMIT_WINDOWS_DAYS", "14");
            Environment.SetEnvironmentVariable("DEVCONTROL_RETENTION_MONITOR_CHECKS_DAYS", "30");
            Environment.SetEnvironmentVariable("DEVCONTROL_RETENTION_WEBHOOK_PREVIEW_DAYS", "30");
            Environment.SetEnvironmentVariable("DEVCONTROL_RETENTION_WEBHOOK_DELIVERIES_DAYS", "90");
            Environment.SetEnvironmentVariable("DEVCONTROL_CLEANUP_BATCH_SIZE", "500");
        }

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

        public async Task<SeededRows> SeedRetentionRowsAsync()
        {
            await using var scope = Services.CreateAsyncScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<DevControlDbContext>();
            var now = DateTimeOffset.UtcNow;
            var old = now.AddDays(-120);
            var recent = now.AddDays(-1);

            var user = new User("owner@example.com", "owner@example.com", "Owner", "test", "owner@example.com", now);
            dbContext.Users.Add(user);
            var organization = new Organization("Acme Platform", "acme-platform", user.Id, now);
            dbContext.Organizations.Add(organization);
            var project = new Project(organization.Id, "Sample App", "sample-app", "Stage 9 cleanup proof", user.Id, now);
            dbContext.Projects.Add(project);
            var environment = new ProjectEnvironment(organization.Id, project.Id, "Production", "production", user.Id, now);
            dbContext.ProjectEnvironments.Add(environment);

            var apiKey = new ApiKey(organization.Id, project.Id, environment.Id, "Runtime key", "dck_test", "hash", "[\"sample:read\"]", 10, user.Id, now);
            dbContext.ApiKeys.Add(apiKey);
            var oldWindow = new ApiKeyRateLimitWindow(apiKey.Id, "/api/runtime/sample/echo", old, old);
            oldWindow.Increment(old);
            var recentWindow = new ApiKeyRateLimitWindow(apiKey.Id, "/api/runtime/sample/echo", recent, recent);
            recentWindow.Increment(recent);
            dbContext.ApiKeyRateLimitWindows.AddRange(oldWindow, recentWindow);

            var monitor = new UptimeMonitor(organization.Id, project.Id, environment.Id, null, "Sample health", "https://sample.example.com/health", false, user.Id, now);
            monitor.Pause(user.Id, now);
            dbContext.UptimeMonitors.Add(monitor);
            var oldCheck = new MonitorCheck(monitor, MonitorStatus.Down, false, 500, "Completed", 100, "old failure", "old body", false, old);
            var recentCheck = new MonitorCheck(monitor, MonitorStatus.Up, true, 200, "Completed", 25, null, "ok", false, recent);
            dbContext.MonitorChecks.AddRange(oldCheck, recentCheck);

            var endpoint = new WebhookEndpoint(organization.Id, project.Id, environment.Id, "Receiver", "https://hooks.example.com/devcontrol", "whsec", "protected", "[\"webhook.test\"]", user.Id, now);
            dbContext.WebhookEndpoints.Add(endpoint);
            var oldEvent = new WebhookEvent(organization.Id, project.Id, environment.Id, "webhook.test", "test", "old", user.Id, user.Email, "{\"old\":true}", old);
            var recentEvent = new WebhookEvent(organization.Id, project.Id, environment.Id, "webhook.test", "test", "recent", user.Id, user.Email, "{\"recent\":true}", recent);
            dbContext.WebhookEvents.AddRange(oldEvent, recentEvent);
            var oldDelivery = new WebhookDelivery(organization.Id, project.Id, environment.Id, endpoint.Id, oldEvent.Id, old);
            oldDelivery.RecordAttempt(false, retryable: false, statusCode: 500, error: "old failure", responsePreview: "old preview", responseTruncated: true, nextAttemptAt: null, old);
            var recentDelivery = new WebhookDelivery(organization.Id, project.Id, environment.Id, endpoint.Id, recentEvent.Id, recent);
            recentDelivery.RecordAttempt(true, retryable: false, statusCode: 200, error: null, responsePreview: "ok", responseTruncated: false, nextAttemptAt: null, recent);
            dbContext.WebhookDeliveries.AddRange(oldDelivery, recentDelivery);
            dbContext.WebhookDeliveryAttempts.AddRange(
                new WebhookDeliveryAttempt(organization.Id, project.Id, environment.Id, endpoint.Id, oldEvent.Id, oldDelivery.Id, 1, "Completed", false, 500, 150, "old failure", "old preview", true, 128, old),
                new WebhookDeliveryAttempt(organization.Id, project.Id, environment.Id, endpoint.Id, recentEvent.Id, recentDelivery.Id, 1, "Completed", true, 200, 20, null, "ok", false, 2, recent));

            var auditLog = new AuditLog(organization.Id, project.Id, environment.Id, user.Id, user.Email, "proof.audit", "Succeeded", "proof", "1", "Old audit log must remain.", "{}", "127.0.0.1", "tests", old);
            dbContext.AuditLogs.Add(auditLog);
            var controlAction = new ControlAction(organization.Id, project.Id, environment.Id, "proof.control", user.Id, "proof", "1", "{}", old);
            controlAction.MarkStarted(old);
            controlAction.MarkCompleted(ControlActionStatus.Succeeded, "{}", null, old);
            dbContext.ControlActions.Add(controlAction);
            var flag = new FeatureFlag(organization.Id, project.Id, environment.Id, "stage9.enabled", "Stage 9", "Durable flag", FeatureFlagKind.FeatureFlag, true, user.Id, old);
            dbContext.FeatureFlags.Add(flag);
            var flagChange = new FeatureFlagChange(flag.Id, organization.Id, project.Id, environment.Id, false, true, "Old flag history must remain.", user.Id, old);
            dbContext.FeatureFlagChanges.Add(flagChange);
            var incident = new Incident(organization.Id, project.Id, environment.Id, "Old incident", "Durable incident", user.Id, old);
            incident.Resolve(user.Id, old);
            dbContext.Incidents.Add(incident);
            var release = new StatusRelease(organization.Id, project.Id, environment.Id, "Old release", "v1", "Durable release", user.Id, old);
            release.Publish(user.Id, old);
            dbContext.StatusReleases.Add(release);
            var liveApp = new LiveApp(organization.Id, project.Id, environment.Id, "fullstack-nick/sample", "fullstack-nick/sample", "https://sample.example.com", "https://sample.example.com/health", "abcdef1234567890", "v1", "sha256:test", "[\"health\"]", null, null, old);
            dbContext.LiveApps.Add(liveApp);
            var deployment = new LiveAppDeployment(liveApp.Id, organization.Id, project.Id, environment.Id, liveApp.Repo, liveApp.ServiceUrl, liveApp.HealthUrl, liveApp.CurrentCommitSha, liveApp.Version, liveApp.ImageDigest, liveApp.CapabilitiesJson, null, null, old);
            dbContext.LiveAppDeployments.Add(deployment);

            await dbContext.SaveChangesAsync();
            return new SeededRows(
                oldWindow.Id,
                recentWindow.Id,
                oldCheck.Id,
                recentCheck.Id,
                oldDelivery.Id,
                recentDelivery.Id,
                auditLog.Id,
                controlAction.Id,
                flagChange.Id,
                incident.Id,
                release.Id,
                deployment.Id);
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Test");
        }

        protected override void Dispose(bool disposing)
        {
            Environment.SetEnvironmentVariable("ConnectionStrings__DevControl", originalConnectionString);
            Environment.SetEnvironmentVariable("DEVCONTROL_SCHEDULER_SECRET", originalSchedulerSecret);
            Environment.SetEnvironmentVariable("DEVCONTROL_METRICS_ENABLED", originalMetricsEnabled);
            Environment.SetEnvironmentVariable("DEVCONTROL_RETENTION_RATE_LIMIT_WINDOWS_DAYS", originalRateLimitDays);
            Environment.SetEnvironmentVariable("DEVCONTROL_RETENTION_MONITOR_CHECKS_DAYS", originalMonitorDays);
            Environment.SetEnvironmentVariable("DEVCONTROL_RETENTION_WEBHOOK_PREVIEW_DAYS", originalWebhookPreviewDays);
            Environment.SetEnvironmentVariable("DEVCONTROL_RETENTION_WEBHOOK_DELIVERIES_DAYS", originalWebhookDeliveryDays);
            Environment.SetEnvironmentVariable("DEVCONTROL_CLEANUP_BATCH_SIZE", originalCleanupBatchSize);
            base.Dispose(disposing);
        }
    }

    private sealed record SeededRows(
        Guid OldRateLimitWindowId,
        Guid RecentRateLimitWindowId,
        Guid OldMonitorCheckId,
        Guid RecentMonitorCheckId,
        Guid OldWebhookDeliveryId,
        Guid RecentWebhookDeliveryId,
        Guid AuditLogId,
        Guid ControlActionId,
        Guid FeatureFlagChangeId,
        Guid IncidentId,
        Guid ReleaseId,
        Guid DeploymentId);
}
