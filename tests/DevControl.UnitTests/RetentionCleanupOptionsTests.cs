using DevControl.Infrastructure.Database;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace DevControl.UnitTests;

public sealed class RetentionCleanupOptionsTests
{
    [Fact]
    public void FromConfiguration_UsesDefaults_WhenValuesAreMissingOrInvalid()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["RETENTION_RATE_LIMIT_WINDOWS_DAYS"] = "not-a-number",
                ["RETENTION_MONITOR_CHECKS_DAYS"] = "",
                ["RETENTION_WEBHOOK_PREVIEW_DAYS"] = "abc",
                ["RETENTION_WEBHOOK_DELIVERIES_DAYS"] = " ",
                ["CLEANUP_BATCH_SIZE"] = "invalid"
            })
            .Build();

        var options = RetentionCleanupOptions.FromConfiguration(configuration);

        Assert.Equal(RetentionCleanupOptions.Defaults, options);
    }

    [Fact]
    public void FromConfiguration_ClampsValues_ToSafeBounds()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["RETENTION_RATE_LIMIT_WINDOWS_DAYS"] = "0",
                ["RETENTION_MONITOR_CHECKS_DAYS"] = "-5",
                ["RETENTION_WEBHOOK_PREVIEW_DAYS"] = "4000",
                ["RETENTION_WEBHOOK_DELIVERIES_DAYS"] = "9999",
                ["CLEANUP_BATCH_SIZE"] = "9000"
            })
            .Build();

        var options = RetentionCleanupOptions.FromConfiguration(configuration);

        Assert.Equal(1, options.RateLimitWindowsDays);
        Assert.Equal(1, options.MonitorChecksDays);
        Assert.Equal(3650, options.WebhookPreviewDays);
        Assert.Equal(3650, options.WebhookDeliveriesDays);
        Assert.Equal(5000, options.BatchSize);
    }
}
