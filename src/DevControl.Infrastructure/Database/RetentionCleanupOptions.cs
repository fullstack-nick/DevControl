using Microsoft.Extensions.Configuration;

namespace DevControl.Infrastructure.Database;

public sealed record RetentionCleanupOptions(
    int RateLimitWindowsDays,
    int MonitorChecksDays,
    int WebhookPreviewDays,
    int WebhookDeliveriesDays,
    int BatchSize)
{
    public const int DefaultRateLimitWindowsDays = 14;
    public const int DefaultMonitorChecksDays = 30;
    public const int DefaultWebhookPreviewDays = 30;
    public const int DefaultWebhookDeliveriesDays = 90;
    public const int DefaultBatchSize = 500;

    private const int MinimumDays = 1;
    private const int MaximumDays = 3650;
    private const int MinimumBatchSize = 1;
    private const int MaximumBatchSize = 5000;

    public static RetentionCleanupOptions Defaults { get; } = new(
        DefaultRateLimitWindowsDays,
        DefaultMonitorChecksDays,
        DefaultWebhookPreviewDays,
        DefaultWebhookDeliveriesDays,
        DefaultBatchSize);

    public static RetentionCleanupOptions FromConfiguration(IConfiguration configuration)
    {
        return new RetentionCleanupOptions(
            ReadBoundedInt(configuration, "RETENTION_RATE_LIMIT_WINDOWS_DAYS", DefaultRateLimitWindowsDays, MinimumDays, MaximumDays),
            ReadBoundedInt(configuration, "RETENTION_MONITOR_CHECKS_DAYS", DefaultMonitorChecksDays, MinimumDays, MaximumDays),
            ReadBoundedInt(configuration, "RETENTION_WEBHOOK_PREVIEW_DAYS", DefaultWebhookPreviewDays, MinimumDays, MaximumDays),
            ReadBoundedInt(configuration, "RETENTION_WEBHOOK_DELIVERIES_DAYS", DefaultWebhookDeliveriesDays, MinimumDays, MaximumDays),
            ReadBoundedInt(configuration, "CLEANUP_BATCH_SIZE", DefaultBatchSize, MinimumBatchSize, MaximumBatchSize));
    }

    private static int ReadBoundedInt(IConfiguration configuration, string key, int defaultValue, int minimum, int maximum)
    {
        var rawValue = configuration[key];
        if (!int.TryParse(rawValue, out var value))
        {
            return defaultValue;
        }

        return Math.Clamp(value, minimum, maximum);
    }
}
