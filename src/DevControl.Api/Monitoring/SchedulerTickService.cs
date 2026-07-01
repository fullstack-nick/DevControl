using DevControl.Api.GitHub;
using DevControl.Api.Webhooks;

namespace DevControl.Api.Monitoring;

public sealed class SchedulerTickService(
    MonitorCheckService monitorCheckService,
    WebhookDeliveryService webhookDeliveryService,
    GitHubSyncService gitHubSyncService)
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
        return new SchedulerTickResult(monitorChecks, webhookRetries, gitHubSync);
    }
}

public sealed record SchedulerTickResult(MonitorCheckBatchResult MonitorChecks, WebhookRetryBatchResult WebhookRetries, GitHubSyncResult GitHubSync);
