using DevControl.Application.Health;
using Xunit;

namespace DevControl.UnitTests;

public sealed class HealthPayloadTests
{
    [Fact]
    public void LivePayload_DoesNotDeclareDependency()
    {
        var payload = HealthPayload.Live();

        Assert.Equal("live", payload.Status);
        Assert.Equal("DevControl", payload.Service);
        Assert.Null(payload.Dependency);
    }

    [Fact]
    public void ReadyPayload_DeclaresPostgreSqlDependency()
    {
        var payload = HealthPayload.Ready();

        Assert.Equal("ready", payload.Status);
        Assert.Equal("postgresql", payload.Dependency);
    }
}
