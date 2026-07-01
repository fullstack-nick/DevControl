using System.Net;
using DevControl.Application.Outbound;
using DevControl.Infrastructure.Outbound;
using Xunit;

namespace DevControl.UnitTests;

public sealed class OutboundRequestGuardTests
{
    [Theory]
    [InlineData("https://example.com/hook", "93.184.216.34")]
    [InlineData("https://api.example.com:443/hook", "8.8.8.8")]
    public async Task ValidateAsync_AllowsPublicHttpsWebhookTargets(string url, string ip)
    {
        var guard = new OutboundRequestGuard(new StaticDnsResolver(IPAddress.Parse(ip)));

        var result = await guard.ValidateAsync(new Uri(url), OutboundRequestPolicy.Webhook, CancellationToken.None);

        Assert.True(result.IsAllowed);
        Assert.Equal(443, result.Port);
    }

    [Theory]
    [InlineData("http://example.com/hook", "93.184.216.34")]
    [InlineData("https://example.com:8443/hook", "93.184.216.34")]
    [InlineData("https://localhost/hook", "127.0.0.1")]
    [InlineData("https://app.localhost/hook", "127.0.0.1")]
    [InlineData("https://metadata.google.internal/hook", "169.254.169.254")]
    [InlineData("https://metadata.goog/hook", "169.254.169.254")]
    [InlineData("https://example.com/hook", "10.0.0.4")]
    [InlineData("https://example.com/hook", "172.16.0.4")]
    [InlineData("https://example.com/hook", "192.168.1.10")]
    [InlineData("https://example.com/hook", "169.254.169.254")]
    [InlineData("https://example.com/hook", "127.0.0.1")]
    [InlineData("https://example.com/hook", "::1")]
    [InlineData("https://example.com/hook", "fc00::1")]
    [InlineData("https://example.com/hook", "fe80::1")]
    public async Task ValidateAsync_BlocksUnsafeWebhookTargets(string url, string ip)
    {
        var guard = new OutboundRequestGuard(new StaticDnsResolver(IPAddress.Parse(ip)));

        var result = await guard.ValidateAsync(new Uri(url), OutboundRequestPolicy.Webhook, CancellationToken.None);

        Assert.False(result.IsAllowed);
        Assert.False(string.IsNullOrWhiteSpace(result.Error));
    }

    [Fact]
    public async Task ValidateAsync_BlocksHostWhenAnyResolvedAddressIsUnsafe()
    {
        var guard = new OutboundRequestGuard(new StaticDnsResolver(
            IPAddress.Parse("93.184.216.34"),
            IPAddress.Parse("10.0.0.10")));

        var result = await guard.ValidateAsync(new Uri("https://example.com/hook"), OutboundRequestPolicy.Webhook, CancellationToken.None);

        Assert.False(result.IsAllowed);
    }

    private sealed class StaticDnsResolver(params IPAddress[] addresses) : IOutboundDnsResolver
    {
        public Task<IReadOnlyList<IPAddress>> ResolveAsync(string host, CancellationToken cancellationToken)
        {
            return Task.FromResult<IReadOnlyList<IPAddress>>(addresses);
        }
    }
}
