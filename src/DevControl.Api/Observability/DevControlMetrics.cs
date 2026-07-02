using DevControl.Api.Monitoring;
using Prometheus;

namespace DevControl.Api.Observability;

public static class DevControlMetrics
{
    private static readonly Counter HttpRequests = Metrics.CreateCounter(
        "devcontrol_http_requests_total",
        "Total HTTP requests handled by DevControl.",
        new CounterConfiguration
        {
            LabelNames = ["method", "route", "status_code"]
        });

    private static readonly Histogram HttpRequestDuration = Metrics.CreateHistogram(
        "devcontrol_http_request_duration_seconds",
        "HTTP request duration in seconds.",
        new HistogramConfiguration
        {
            Buckets = Histogram.ExponentialBuckets(0.005, 2, 12),
            LabelNames = ["method", "route", "status_code"]
        });

    private static readonly Counter SchedulerBatches = Metrics.CreateCounter(
        "devcontrol_scheduler_batches_total",
        "Scheduler batches executed by component.",
        new CounterConfiguration
        {
            LabelNames = ["component"]
        });

    private static readonly Counter SchedulerItems = Metrics.CreateCounter(
        "devcontrol_scheduler_items_processed_total",
        "Scheduler items processed by component.",
        new CounterConfiguration
        {
            LabelNames = ["component"]
        });

    private static readonly Gauge SchedulerBatchSize = Metrics.CreateGauge(
        "devcontrol_scheduler_batch_size",
        "Configured scheduler batch size by component.",
        new GaugeConfiguration
        {
            LabelNames = ["component"]
        });

    private static readonly Counter MonitorChecks = Metrics.CreateCounter(
        "devcontrol_monitor_checks_total",
        "Monitor checks recorded by status and result kind.",
        new CounterConfiguration
        {
            LabelNames = ["status", "result_kind"]
        });

    private static readonly Histogram MonitorCheckDuration = Metrics.CreateHistogram(
        "devcontrol_monitor_check_duration_seconds",
        "Monitor check duration in seconds.",
        new HistogramConfiguration
        {
            Buckets = Histogram.ExponentialBuckets(0.01, 2, 12),
            LabelNames = ["status", "result_kind"]
        });

    private static readonly Counter WebhookDeliveryAttempts = Metrics.CreateCounter(
        "devcontrol_webhook_delivery_attempts_total",
        "Webhook delivery attempts by final delivery status and outbound result kind.",
        new CounterConfiguration
        {
            LabelNames = ["status", "result_kind", "succeeded"]
        });

    private static readonly Histogram WebhookDeliveryAttemptDuration = Metrics.CreateHistogram(
        "devcontrol_webhook_delivery_attempt_duration_seconds",
        "Webhook delivery attempt duration in seconds.",
        new HistogramConfiguration
        {
            Buckets = Histogram.ExponentialBuckets(0.01, 2, 12),
            LabelNames = ["status", "result_kind"]
        });

    private static readonly Counter RuntimeApiKeyRequests = Metrics.CreateCounter(
        "devcontrol_api_key_runtime_requests_total",
        "Runtime API key requests by endpoint and outcome.",
        new CounterConfiguration
        {
            LabelNames = ["endpoint", "outcome"]
        });

    private static readonly Counter RuntimeApiKeyRateLimitHits = Metrics.CreateCounter(
        "devcontrol_api_key_rate_limit_hits_total",
        "Runtime API key requests rejected by rate limiting.",
        new CounterConfiguration
        {
            LabelNames = ["endpoint"]
        });

    private static readonly Counter GitHubSyncItems = Metrics.CreateCounter(
        "devcontrol_github_sync_items_total",
        "GitHub sync rows processed by component.",
        new CounterConfiguration
        {
            LabelNames = ["component"]
        });

    private static readonly Counter CleanupChanges = Metrics.CreateCounter(
        "devcontrol_cleanup_changes_total",
        "Retention cleanup rows changed by operation.",
        new CounterConfiguration
        {
            LabelNames = ["operation"]
        });

    public static void RecordHttpRequest(string method, string? path, int statusCode, TimeSpan elapsed)
    {
        var route = ToRouteBucket(path);
        var status = statusCode.ToString();
        var normalizedMethod = NormalizeMethod(method);
        HttpRequests.WithLabels(normalizedMethod, route, status).Inc();
        HttpRequestDuration.WithLabels(normalizedMethod, route, status).Observe(elapsed.TotalSeconds);
    }

    public static void RecordSchedulerResult(SchedulerTickResult result)
    {
        RecordSchedulerBatch("monitor_checks", result.MonitorChecks.Processed, result.MonitorChecks.BatchSize);
        RecordSchedulerBatch("webhook_retries", result.WebhookRetries.Processed, result.WebhookRetries.BatchSize);
        RecordSchedulerBatch("github_pull_requests", result.GitHubSync.PullRequests, result.GitHubSync.PullRequestBatchSize);
        RecordSchedulerBatch("github_workflow_dispatches", result.GitHubSync.WorkflowDispatches, result.GitHubSync.WorkflowDispatchBatchSize);
        RecordSchedulerBatch("retention_cleanup", result.Cleanup.TotalChanged, result.Cleanup.BatchSize);

        GitHubSyncItems.WithLabels("pull_requests").Inc(result.GitHubSync.PullRequests);
        GitHubSyncItems.WithLabels("workflow_dispatches").Inc(result.GitHubSync.WorkflowDispatches);
        RecordCleanup(result.Cleanup);
    }

    public static void RecordMonitorCheck(string status, string resultKind, TimeSpan elapsed)
    {
        var normalizedStatus = NormalizeLabel(status, "Unknown");
        var normalizedResult = NormalizeLabel(resultKind, "Unknown");
        MonitorChecks.WithLabels(normalizedStatus, normalizedResult).Inc();
        MonitorCheckDuration.WithLabels(normalizedStatus, normalizedResult).Observe(elapsed.TotalSeconds);
    }

    public static void RecordWebhookDeliveryAttempt(string status, string resultKind, bool succeeded, TimeSpan elapsed)
    {
        var normalizedStatus = NormalizeLabel(status, "Unknown");
        var normalizedResult = NormalizeLabel(resultKind, "Unknown");
        WebhookDeliveryAttempts.WithLabels(normalizedStatus, normalizedResult, succeeded ? "true" : "false").Inc();
        WebhookDeliveryAttemptDuration.WithLabels(normalizedStatus, normalizedResult).Observe(elapsed.TotalSeconds);
    }

    public static void RecordRuntimeApiKeyRequest(string endpoint, string outcome)
    {
        RuntimeApiKeyRequests.WithLabels(NormalizeEndpoint(endpoint), NormalizeLabel(outcome, "unknown")).Inc();
    }

    public static void RecordRuntimeApiKeyRateLimitHit(string endpoint)
    {
        RuntimeApiKeyRateLimitHits.WithLabels(NormalizeEndpoint(endpoint)).Inc();
    }

    public static string ToRouteBucket(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || path == "/")
        {
            return "/";
        }

        if (path is "/health/live" or "/health/ready" or "/metrics")
        {
            return path;
        }

        if (IsStaticAsset(path))
        {
            return "static";
        }

        var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (segments.Length == 0)
        {
            return "/";
        }

        if (segments is ["api", "public", "status", _, ..])
        {
            return "/api/public/status/{organizationSlug}/{projectSlug}";
        }

        if (segments is ["api", "runtime", "sample", "echo"])
        {
            return "/api/runtime/sample/echo";
        }

        if (segments is ["api", "apps", "register"])
        {
            return "/api/apps/register";
        }

        if (segments.Length >= 3 && segments[0] == "api" && segments[1] == "organizations")
        {
            var boundedSegments = segments
                .Select((segment, index) => index == 2 || Guid.TryParse(segment, out _) ? "{id}" : segment)
                .Take(7);
            return "/" + string.Join("/", boundedSegments);
        }

        if (segments[0] is "api" or "auth" or "status")
        {
            return "/" + string.Join("/", segments.Select(segment => Guid.TryParse(segment, out _) ? "{id}" : segment).Take(5));
        }

        return "other";
    }

    private static void RecordSchedulerBatch(string component, int processed, int batchSize)
    {
        SchedulerBatches.WithLabels(component).Inc();
        SchedulerItems.WithLabels(component).Inc(processed);
        SchedulerBatchSize.WithLabels(component).Set(batchSize);
    }

    private static void RecordCleanup(RetentionCleanupResult cleanup)
    {
        CleanupChanges.WithLabels("api_key_rate_limit_windows_deleted").Inc(cleanup.ApiKeyRateLimitWindowsDeleted);
        CleanupChanges.WithLabels("monitor_checks_deleted").Inc(cleanup.MonitorChecksDeleted);
        CleanupChanges.WithLabels("webhook_delivery_attempts_compacted").Inc(cleanup.WebhookDeliveryAttemptsCompacted);
        CleanupChanges.WithLabels("webhook_deliveries_compacted").Inc(cleanup.WebhookDeliveriesCompacted);
        CleanupChanges.WithLabels("webhook_delivery_attempts_deleted").Inc(cleanup.WebhookDeliveryAttemptsDeleted);
        CleanupChanges.WithLabels("webhook_deliveries_deleted").Inc(cleanup.WebhookDeliveriesDeleted);
        CleanupChanges.WithLabels("webhook_events_deleted").Inc(cleanup.WebhookEventsDeleted);
    }

    private static string NormalizeMethod(string method)
    {
        return method.ToUpperInvariant() switch
        {
            "GET" => "GET",
            "POST" => "POST",
            "PUT" => "PUT",
            "PATCH" => "PATCH",
            "DELETE" => "DELETE",
            "HEAD" => "HEAD",
            "OPTIONS" => "OPTIONS",
            _ => "OTHER"
        };
    }

    private static string NormalizeEndpoint(string endpoint)
    {
        return endpoint == "/api/runtime/sample/echo" ? endpoint : "other";
    }

    private static string NormalizeLabel(string value, string fallback)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return fallback;
        }

        var trimmed = value.Trim();
        return trimmed.Length <= 80 ? trimmed : trimmed[..80];
    }

    private static bool IsStaticAsset(string path)
    {
        return path.StartsWith("/assets/", StringComparison.OrdinalIgnoreCase) ||
            path.EndsWith(".js", StringComparison.OrdinalIgnoreCase) ||
            path.EndsWith(".css", StringComparison.OrdinalIgnoreCase) ||
            path.EndsWith(".ico", StringComparison.OrdinalIgnoreCase) ||
            path.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ||
            path.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) ||
            path.EndsWith(".svg", StringComparison.OrdinalIgnoreCase) ||
            path.EndsWith(".webp", StringComparison.OrdinalIgnoreCase);
    }
}
