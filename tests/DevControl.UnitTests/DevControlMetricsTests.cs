using DevControl.Api.Observability;
using Xunit;

namespace DevControl.UnitTests;

public sealed class DevControlMetricsTests
{
    [Theory]
    [InlineData("/api/organizations/9d1746f4-25b2-49d7-ac0d-bdf3bd49e3a2/monitors", "/api/organizations/{id}/monitors")]
    [InlineData("/api/public/status/acme-platform/sample-app", "/api/public/status/{organizationSlug}/{projectSlug}")]
    [InlineData("/assets/index-abc123.js", "static")]
    [InlineData("/api/runtime/sample/echo", "/api/runtime/sample/echo")]
    public void ToRouteBucket_BoundsHighCardinalityRouteValues(string path, string expected)
    {
        Assert.Equal(expected, DevControlMetrics.ToRouteBucket(path));
    }

    [Fact]
    public void RecordMethods_CanBeCalledRepeatedly()
    {
        DevControlMetrics.RecordHttpRequest("GET", "/health/live", 200, TimeSpan.FromMilliseconds(3));
        DevControlMetrics.RecordHttpRequest("GET", "/health/live", 200, TimeSpan.FromMilliseconds(4));
        DevControlMetrics.RecordMonitorCheck("Up", "Completed", TimeSpan.FromMilliseconds(10));
        DevControlMetrics.RecordMonitorCheck("Up", "Completed", TimeSpan.FromMilliseconds(11));
        DevControlMetrics.RecordWebhookDeliveryAttempt("Succeeded", "Completed", succeeded: true, TimeSpan.FromMilliseconds(20));
        DevControlMetrics.RecordWebhookDeliveryAttempt("Succeeded", "Completed", succeeded: true, TimeSpan.FromMilliseconds(21));
        DevControlMetrics.RecordRuntimeApiKeyRequest("/api/runtime/sample/echo", "status_200");
        DevControlMetrics.RecordRuntimeApiKeyRateLimitHit("/api/runtime/sample/echo");
    }
}
