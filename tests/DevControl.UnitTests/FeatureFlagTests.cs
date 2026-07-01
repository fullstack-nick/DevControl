using DevControl.Application.Security;
using DevControl.Domain.Entities;
using DevControl.Domain.Enums;
using Xunit;

namespace DevControl.UnitTests;

public sealed class FeatureFlagTests
{
    [Fact]
    public void FeatureFlagKeys_NormalizesAndRejectsInvalidKeys()
    {
        Assert.True(FeatureFlagKeys.TryNormalize(" Checkout.New_Flow ", out var key, out var error));
        Assert.Equal("checkout.new_flow", key);
        Assert.Null(error);

        Assert.False(FeatureFlagKeys.TryNormalize("bad key", out _, out var invalidError));
        Assert.Contains("lowercase letters", invalidError, StringComparison.Ordinal);
    }

    [Fact]
    public void FeatureFlag_UpdateTracksValueAndMetadata()
    {
        var now = DateTimeOffset.Parse("2026-07-01T12:00:00Z");
        var actor = Guid.NewGuid();
        var flag = new FeatureFlag(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            "checkout.enabled",
            "Checkout",
            "Controls checkout",
            FeatureFlagKind.FeatureFlag,
            isEnabled: false,
            actor,
            now);

        flag.Update("Checkout v2", "Updated", true, Guid.NewGuid(), now.AddMinutes(5));

        Assert.Equal("Checkout v2", flag.Name);
        Assert.Equal("Updated", flag.Description);
        Assert.True(flag.IsEnabled);
        Assert.Equal(now.AddMinutes(5), flag.LastChangedAt);
        Assert.Equal(now.AddMinutes(5), flag.UpdatedAt);
    }
}
