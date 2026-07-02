using DevControl.Domain.Enums;
using DevControl.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;

namespace DevControl.Api.Monitoring;

public sealed class RetentionCleanupService(
    DevControlDbContext dbContext,
    RetentionCleanupOptions options,
    TimeProvider timeProvider,
    ILogger<RetentionCleanupService> logger)
{
    public async Task<RetentionCleanupResult> RunAsync(CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var rateLimitWindowCutoff = now.AddDays(-options.RateLimitWindowsDays);
        var monitorCheckCutoff = now.AddDays(-options.MonitorChecksDays);
        var webhookPreviewCutoff = now.AddDays(-options.WebhookPreviewDays);
        var webhookDeliveryCutoff = now.AddDays(-options.WebhookDeliveriesDays);

        var deletedRateLimitWindows = await DeleteRateLimitWindowsAsync(rateLimitWindowCutoff, cancellationToken);
        var deletedMonitorChecks = await DeleteMonitorChecksAsync(monitorCheckCutoff, cancellationToken);
        var compactedWebhookAttempts = await CompactWebhookAttemptsAsync(webhookPreviewCutoff, cancellationToken);
        var compactedWebhookDeliveries = await CompactWebhookDeliveriesAsync(webhookPreviewCutoff, now, cancellationToken);
        var oldDeliveryIds = await LoadOldTerminalWebhookDeliveryIdsAsync(webhookDeliveryCutoff, cancellationToken);
        var deletedWebhookAttempts = await DeleteWebhookAttemptsAsync(oldDeliveryIds, cancellationToken);
        var deletedWebhookDeliveries = await DeleteWebhookDeliveriesAsync(oldDeliveryIds, cancellationToken);
        var deletedWebhookEvents = await DeleteOrphanWebhookEventsAsync(webhookDeliveryCutoff, cancellationToken);

        var result = new RetentionCleanupResult(
            deletedRateLimitWindows,
            deletedMonitorChecks,
            compactedWebhookAttempts,
            compactedWebhookDeliveries,
            deletedWebhookAttempts,
            deletedWebhookDeliveries,
            deletedWebhookEvents,
            options.BatchSize);

        logger.LogInformation(
            "Retention cleanup completed: {ApiKeyRateLimitWindowsDeleted} rate windows, {MonitorChecksDeleted} monitor checks, {WebhookDeliveryAttemptsCompacted} webhook attempts compacted, {WebhookDeliveriesCompacted} webhook deliveries compacted, {WebhookDeliveryAttemptsDeleted} webhook attempts deleted, {WebhookDeliveriesDeleted} webhook deliveries deleted, {WebhookEventsDeleted} webhook events deleted.",
            result.ApiKeyRateLimitWindowsDeleted,
            result.MonitorChecksDeleted,
            result.WebhookDeliveryAttemptsCompacted,
            result.WebhookDeliveriesCompacted,
            result.WebhookDeliveryAttemptsDeleted,
            result.WebhookDeliveriesDeleted,
            result.WebhookEventsDeleted);

        return result;
    }

    private async Task<int> DeleteRateLimitWindowsAsync(DateTimeOffset cutoff, CancellationToken cancellationToken)
    {
        var ids = await dbContext.ApiKeyRateLimitWindows
            .Where(window => window.WindowStart < cutoff)
            .OrderBy(window => window.WindowStart)
            .Take(options.BatchSize)
            .Select(window => window.Id)
            .ToListAsync(cancellationToken);

        if (ids.Count == 0)
        {
            return 0;
        }

        return await dbContext.ApiKeyRateLimitWindows
            .Where(window => ids.Contains(window.Id))
            .ExecuteDeleteAsync(cancellationToken);
    }

    private async Task<int> DeleteMonitorChecksAsync(DateTimeOffset cutoff, CancellationToken cancellationToken)
    {
        var ids = await dbContext.MonitorChecks
            .Where(check => check.CheckedAt < cutoff)
            .OrderBy(check => check.CheckedAt)
            .Take(options.BatchSize)
            .Select(check => check.Id)
            .ToListAsync(cancellationToken);

        if (ids.Count == 0)
        {
            return 0;
        }

        return await dbContext.MonitorChecks
            .Where(check => ids.Contains(check.Id))
            .ExecuteDeleteAsync(cancellationToken);
    }

    private async Task<int> CompactWebhookAttemptsAsync(DateTimeOffset cutoff, CancellationToken cancellationToken)
    {
        var ids = await dbContext.WebhookDeliveryAttempts
            .Where(attempt =>
                attempt.CreatedAt < cutoff &&
                (attempt.Error != string.Empty || attempt.ResponsePreview != string.Empty || attempt.ResponseTruncated))
            .OrderBy(attempt => attempt.CreatedAt)
            .Take(options.BatchSize)
            .Select(attempt => attempt.Id)
            .ToListAsync(cancellationToken);

        if (ids.Count == 0)
        {
            return 0;
        }

        return await dbContext.WebhookDeliveryAttempts
            .Where(attempt => ids.Contains(attempt.Id))
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(attempt => attempt.Error, string.Empty)
                    .SetProperty(attempt => attempt.ResponsePreview, string.Empty)
                    .SetProperty(attempt => attempt.ResponseTruncated, false),
                cancellationToken);
    }

    private async Task<int> CompactWebhookDeliveriesAsync(DateTimeOffset cutoff, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var ids = await dbContext.WebhookDeliveries
            .Where(delivery =>
                delivery.CompletedAt != null &&
                delivery.CompletedAt < cutoff &&
                (delivery.Status == WebhookDeliveryStatus.Succeeded ||
                    delivery.Status == WebhookDeliveryStatus.Exhausted ||
                    delivery.Status == WebhookDeliveryStatus.SkippedPaused) &&
                (delivery.LastError != string.Empty || delivery.LastResponsePreview != string.Empty || delivery.LastResponseTruncated))
            .OrderBy(delivery => delivery.CompletedAt)
            .Take(options.BatchSize)
            .Select(delivery => delivery.Id)
            .ToListAsync(cancellationToken);

        if (ids.Count == 0)
        {
            return 0;
        }

        return await dbContext.WebhookDeliveries
            .Where(delivery => ids.Contains(delivery.Id))
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(delivery => delivery.LastError, string.Empty)
                    .SetProperty(delivery => delivery.LastResponsePreview, string.Empty)
                    .SetProperty(delivery => delivery.LastResponseTruncated, false)
                    .SetProperty(delivery => delivery.UpdatedAt, now),
                cancellationToken);
    }

    private async Task<IReadOnlyList<Guid>> LoadOldTerminalWebhookDeliveryIdsAsync(DateTimeOffset cutoff, CancellationToken cancellationToken)
    {
        return await dbContext.WebhookDeliveries
            .Where(delivery =>
                delivery.CompletedAt != null &&
                delivery.CompletedAt < cutoff &&
                (delivery.Status == WebhookDeliveryStatus.Succeeded ||
                    delivery.Status == WebhookDeliveryStatus.Exhausted ||
                    delivery.Status == WebhookDeliveryStatus.SkippedPaused))
            .OrderBy(delivery => delivery.CompletedAt)
            .Take(options.BatchSize)
            .Select(delivery => delivery.Id)
            .ToListAsync(cancellationToken);
    }

    private async Task<int> DeleteWebhookAttemptsAsync(IReadOnlyList<Guid> deliveryIds, CancellationToken cancellationToken)
    {
        if (deliveryIds.Count == 0)
        {
            return 0;
        }

        return await dbContext.WebhookDeliveryAttempts
            .Where(attempt => deliveryIds.Contains(attempt.WebhookDeliveryId))
            .ExecuteDeleteAsync(cancellationToken);
    }

    private async Task<int> DeleteWebhookDeliveriesAsync(IReadOnlyList<Guid> deliveryIds, CancellationToken cancellationToken)
    {
        if (deliveryIds.Count == 0)
        {
            return 0;
        }

        return await dbContext.WebhookDeliveries
            .Where(delivery => deliveryIds.Contains(delivery.Id))
            .ExecuteDeleteAsync(cancellationToken);
    }

    private async Task<int> DeleteOrphanWebhookEventsAsync(DateTimeOffset cutoff, CancellationToken cancellationToken)
    {
        var ids = await dbContext.WebhookEvents
            .Where(webhookEvent =>
                webhookEvent.CreatedAt < cutoff &&
                !dbContext.WebhookDeliveries.Any(delivery => delivery.WebhookEventId == webhookEvent.Id))
            .OrderBy(webhookEvent => webhookEvent.CreatedAt)
            .Take(options.BatchSize)
            .Select(webhookEvent => webhookEvent.Id)
            .ToListAsync(cancellationToken);

        if (ids.Count == 0)
        {
            return 0;
        }

        return await dbContext.WebhookEvents
            .Where(webhookEvent => ids.Contains(webhookEvent.Id))
            .ExecuteDeleteAsync(cancellationToken);
    }

}

public sealed record RetentionCleanupResult(
    int ApiKeyRateLimitWindowsDeleted,
    int MonitorChecksDeleted,
    int WebhookDeliveryAttemptsCompacted,
    int WebhookDeliveriesCompacted,
    int WebhookDeliveryAttemptsDeleted,
    int WebhookDeliveriesDeleted,
    int WebhookEventsDeleted,
    int BatchSize)
{
    public int TotalChanged =>
        ApiKeyRateLimitWindowsDeleted +
        MonitorChecksDeleted +
        WebhookDeliveryAttemptsCompacted +
        WebhookDeliveriesCompacted +
        WebhookDeliveryAttemptsDeleted +
        WebhookDeliveriesDeleted +
        WebhookEventsDeleted;
}
