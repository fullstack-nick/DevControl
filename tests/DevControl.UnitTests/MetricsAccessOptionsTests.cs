using DevControl.Api.Observability;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace DevControl.UnitTests;

public sealed class MetricsAccessOptionsTests
{
    [Fact]
    public void FromConfiguration_AllowsTokenlessMetrics_InDevelopment()
    {
        var options = MetricsAccessOptions.FromConfiguration(
            Configuration(("METRICS_ENABLED", "true")),
            new TestHostEnvironment(Environments.Development));

        Assert.True(options.Enabled);
        Assert.False(options.RequiresToken);
        Assert.True(options.IsAuthorized(null));
    }

    [Fact]
    public void FromConfiguration_RequiresScrapeToken_OutsideDevelopmentAndTest()
    {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            MetricsAccessOptions.FromConfiguration(
                Configuration(("METRICS_ENABLED", "true")),
                new TestHostEnvironment(Environments.Production)));

        Assert.Equal(MetricsAccessOptions.MissingProductionTokenMessage, exception.Message);
    }

    [Fact]
    public void IsAuthorized_RequiresMatchingBearerToken_WhenScrapeTokenConfigured()
    {
        var options = MetricsAccessOptions.FromConfiguration(
            Configuration(
                ("METRICS_ENABLED", "true"),
                ("METRICS_SCRAPE_TOKEN", "live-token")),
            new TestHostEnvironment(Environments.Production));

        Assert.True(options.RequiresToken);
        Assert.False(options.IsAuthorized(null));
        Assert.False(options.IsAuthorized("Bearer wrong-token"));
        Assert.False(options.IsAuthorized("Basic live-token"));
        Assert.True(options.IsAuthorized("Bearer live-token"));
    }

    private static IConfiguration Configuration(params (string Key, string Value)[] values)
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(values.Select(value =>
                new KeyValuePair<string, string?>(value.Key, value.Value)))
            .Build();
    }

    private sealed class TestHostEnvironment(string environmentName) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = environmentName;

        public string ApplicationName { get; set; } = "DevControl.Tests";

        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;

        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
