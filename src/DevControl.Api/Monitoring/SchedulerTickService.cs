using DevControl.Api.GitHub;
using DevControl.Api.Observability;
using DevControl.Api.Webhooks;

namespace DevControl.Api.Monitoring;

public sealed class SchedulerTickService(
    MonitorCheckService monitorCheckService,
    WebhookDeliveryService webhookDeliveryService,
    GitHubSyncService gitHubSyncService,
    RetentionCleanupService retentionCleanupService,
    ILogger<SchedulerTickService> logger)
{
    private const int MonitorBatchSize = 20;
    private const int WebhookRetryBatchSize = 25;
    private const int GitHubPullRequestBatchSize = 10;
    private const int GitHubDispatchBatchSize = 10;

    public async Task<SchedulerTickResult> RunAsync(CancellationToken cancellationToken)
    {
        var monitorChecks = await monitorCheckService.ProcessDueChecksAsync(MonitorBatchSize, cancellationToken);
        var webhookRetries = await webhookDeliveryService.ProcessDueRetriesAsync(WebhookRetryBatchSize, cancellationToken);
        var gitHubSync = await gitHubSyncService.SyncAsync(GitHubPullRequestBatchSize, GitHubDispatchBatchSize, cancellationToken);
        var cleanup = await retentionCleanupService.RunAsync(cancellationToken);
        var result = new SchedulerTickResult(monitorChecks, webhookRetries, gitHubSync, cleanup);
        DevControlMetrics.RecordSchedulerResult(result);
        logger.LogInformation(
            "Scheduler tick completed: {MonitorChecksProcessed} monitor checks, {WebhookRetriesProcessed} webhook retries, {GitHubPullRequestsSynced} GitHub pull requests, {GitHubDispatchesSynced} GitHub dispatches, {CleanupRowsChanged} cleanup rows changed.",
            result.MonitorChecks.Processed,
            result.WebhookRetries.Processed,
            result.GitHubSync.PullRequests,
            result.GitHubSync.WorkflowDispatches,
            result.Cleanup.TotalChanged);
        return result;
    }
}

public sealed record SchedulerTickResult(
    MonitorCheckBatchResult MonitorChecks,
    WebhookRetryBatchResult WebhookRetries,
    GitHubSyncResult GitHubSync,
    RetentionCleanupResult Cleanup);
