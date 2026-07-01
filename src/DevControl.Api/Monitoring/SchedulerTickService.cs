using DevControl.Api.Webhooks;

namespace DevControl.Api.Monitoring;

public sealed class SchedulerTickService(
    MonitorCheckService monitorCheckService,
    WebhookDeliveryService webhookDeliveryService)
{
    private const int MonitorBatchSize = 20;
    private const int WebhookRetryBatchSize = 25;

    public async Task<SchedulerTickResult> RunAsync(CancellationToken cancellationToken)
    {
        var monitorChecks = await monitorCheckService.ProcessDueChecksAsync(MonitorBatchSize, cancellationToken);
        var webhookRetries = await webhookDeliveryService.ProcessDueRetriesAsync(WebhookRetryBatchSize, cancellationToken);
        return new SchedulerTickResult(monitorChecks, webhookRetries);
    }
}

public sealed record SchedulerTickResult(MonitorCheckBatchResult MonitorChecks, WebhookRetryBatchResult WebhookRetries);
