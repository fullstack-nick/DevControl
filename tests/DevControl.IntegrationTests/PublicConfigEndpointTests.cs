using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace DevControl.IntegrationTests;

[Collection(PostgresIntegrationCollection.Name)]
public sealed class PublicConfigEndpointTests
{
    [Fact]
    public async Task PublicConfig_ReturnsConfiguredObservabilityUrl()
    {
        await using var factory = new DevControlPublicConfigFactory("https://grafana.example.test/");
        using var client = factory.CreateClient();

        var config = await client.GetFromJsonAsync<PublicConfigDto>("/api/public/config");

        Assert.Equal("https://grafana.example.test", config?.ObservabilityUrl);
    }

    [Fact]
    public async Task PublicConfig_DerivesCloudRunObservabilityUrl()
    {
        await using var factory = new DevControlPublicConfigFactory(null);
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/public/config");
        request.Headers.Host = "devcontrol-nictbzfhga-uc.a.run.app";

        var response = await client.SendAsync(request);
        var config = await response.Content.ReadFromJsonAsync<PublicConfigDto>();

        Assert.Equal("https://devcontrol-observability-nictbzfhga-uc.a.run.app", config?.ObservabilityUrl);
    }

    private sealed class DevControlPublicConfigFactory : WebApplicationFactory<Program>
    {
        private readonly string? originalConnectionString;
        private readonly string? originalObservabilityUrl;

        public DevControlPublicConfigFactory(string? observabilityUrl)
        {
            originalConnectionString = Environment.GetEnvironmentVariable("ConnectionStrings__DevControl");
            originalObservabilityUrl = Environment.GetEnvironmentVariable("DEVCONTROL_OBSERVABILITY_URL");
            Environment.SetEnvironmentVariable("ConnectionStrings__DevControl", "Host=127.0.0.1;Port=65432;Database=missing;Username=missing;Password=missing");
            Environment.SetEnvironmentVariable("DEVCONTROL_OBSERVABILITY_URL", observabilityUrl);
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Test");
        }

        protected override void Dispose(bool disposing)
        {
            Environment.SetEnvironmentVariable("ConnectionStrings__DevControl", originalConnectionString);
            Environment.SetEnvironmentVariable("DEVCONTROL_OBSERVABILITY_URL", originalObservabilityUrl);
            base.Dispose(disposing);
        }
    }

    private sealed record PublicConfigDto(string? ObservabilityUrl);
}
