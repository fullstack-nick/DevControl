using System.Net;
using DevControl.Sdk;
using Xunit;

namespace DevControl.UnitTests;

public sealed class DevControlSdkTests
{
    [Fact]
    public async Task RefreshCachesSnapshotAndLocalEvaluationDoesNotCallNetwork()
    {
        var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Headers = { ETag = new("\"v1\"") },
            Content = new StringContent("""
                {
                  "version": "v1",
                  "generatedAt": "2026-07-01T12:00:00Z",
                  "refreshIntervalSeconds": 60,
                  "killSwitchRefreshIntervalSeconds": 20,
                  "flags": { "checkout.enabled": true },
                  "killSwitches": { "checkout.kill": false }
                }
                """)
        });
        using var client = new DevControlClient(
            new Uri("https://devcontrol.example.com"),
            "dck_test",
            new HttpClient(handler),
            new DevControlClientOptions { RefreshInterval = TimeSpan.FromMinutes(1) });

        var result = await client.RefreshAsync();

        Assert.Equal(DevControlRefreshStatus.Updated, result.Status);
        Assert.True(client.IsEnabled("checkout.enabled"));
        Assert.False(client.IsKilled("checkout.kill"));
        Assert.Equal(1, handler.RequestCount);

        Assert.True(client.IsEnabled("checkout.enabled"));
        Assert.True(client.IsEnabled("missing", defaultValue: true));
        Assert.Equal(1, handler.RequestCount);
    }

    [Fact]
    public async Task RefreshUsesEtagAndHandlesNotModified()
    {
        var handler = new RecordingHandler(request =>
        {
            if (request.Headers.IfNoneMatch.Count > 0)
            {
                return new HttpResponseMessage(HttpStatusCode.NotModified);
            }

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Headers = { ETag = new("\"v1\"") },
                Content = new StringContent("""
                    {
                      "version": "v1",
                      "generatedAt": "2026-07-01T12:00:00Z",
                      "refreshIntervalSeconds": 60,
                      "killSwitchRefreshIntervalSeconds": 20,
                      "flags": { "checkout.enabled": true },
                      "killSwitches": {}
                    }
                    """)
            };
        });
        using var client = new DevControlClient(new Uri("https://devcontrol.example.com"), "dck_test", new HttpClient(handler));

        var first = await client.RefreshAsync();
        var second = await client.RefreshAsync();

        Assert.Equal(DevControlRefreshStatus.Updated, first.Status);
        Assert.Equal(DevControlRefreshStatus.NotModified, second.Status);
        Assert.Equal("\"v1\"", handler.LastRequest!.Headers.IfNoneMatch.Single().ToString());
        Assert.Equal(2, handler.RequestCount);
    }

    [Fact]
    public async Task RefreshFailureKeepsStaleSnapshotAndKillSwitchDefaultsFailSafe()
    {
        var fail = false;
        var handler = new RecordingHandler(_ =>
        {
            if (fail)
            {
                return new HttpResponseMessage(HttpStatusCode.BadGateway);
            }

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""
                    {
                      "version": "v1",
                      "generatedAt": "2026-07-01T12:00:00Z",
                      "refreshIntervalSeconds": 60,
                      "killSwitchRefreshIntervalSeconds": 20,
                      "flags": { "feature": true },
                      "killSwitches": { "kill": false }
                    }
                    """)
            };
        });
        using var client = new DevControlClient(new Uri("https://devcontrol.example.com"), "dck_test", new HttpClient(handler));

        await client.RefreshAsync();
        fail = true;
        var failed = await client.RefreshAsync();

        Assert.Equal(DevControlRefreshStatus.Failed, failed.Status);
        Assert.True(client.IsEnabled("feature"));
        Assert.False(client.IsKilled("kill"));
        Assert.True(client.IsKilled("missing"));
    }

    private sealed class RecordingHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
    {
        public int RequestCount { get; private set; }

        public HttpRequestMessage? LastRequest { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestCount++;
            LastRequest = request;
            return Task.FromResult(responder(request));
        }
    }
}
