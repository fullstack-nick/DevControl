using DevControl.Application.Security;
using DevControl.Domain.Entities;
using Xunit;

namespace DevControl.UnitTests;

public sealed class ApiKeySecurityTests
{
    [Fact]
    public void ApiKeySecretService_HashesAndPrefixesKey()
    {
        var service = new ApiKeySecretService();

        var key = service.CreateKey();

        Assert.StartsWith("dck_", key.Secret, StringComparison.Ordinal);
        Assert.Equal(key.Secret[..16], key.Prefix);
        Assert.Equal(64, key.Hash.Length);
        Assert.Equal(key.Hash, service.HashKey(key.Secret));
        Assert.DoesNotContain(key.Secret, key.Hash, StringComparison.Ordinal);
    }

    [Fact]
    public void ApiKeyScopes_DefaultNormalizeAndRejectUnsupportedScopes()
    {
        Assert.True(ApiKeyScopes.TryNormalize(null, out var defaultScopes, out var defaultJson, out var defaultErrors));
        Assert.Equal([ApiKeyScopes.SampleRead], defaultScopes);
        Assert.Equal("[\"sample:read\"]", defaultJson);
        Assert.Empty(defaultErrors);

        Assert.False(ApiKeyScopes.TryNormalize(["sample:read", "unknown"], out _, out _, out var errors));
        Assert.Contains(errors, error => error.Contains("Unsupported API key scope", StringComparison.Ordinal));
    }

    [Fact]
    public void RateLimitWindow_TracksAllowedRequestsAndHits()
    {
        var now = DateTimeOffset.Parse("2026-06-30T12:00:00Z");
        var window = new ApiKeyRateLimitWindow(Guid.NewGuid(), "/api/runtime/sample/echo", now, now);

        window.Increment(now);
        window.Increment(now.AddSeconds(1));
        window.MarkRateLimitHit(now.AddSeconds(2));

        Assert.Equal(2, window.RequestCount);
        Assert.Equal(1, window.RateLimitHitCount);
        Assert.Equal(now.AddSeconds(2), window.UpdatedAt);
    }
}
