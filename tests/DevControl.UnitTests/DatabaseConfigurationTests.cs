using DevControl.Infrastructure.Database;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace DevControl.UnitTests;

public sealed class DatabaseConfigurationTests
{
    [Fact]
    public void GetConnectionString_UsesExplicitConnectionString()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DevControl"] = "Host=db;Database=devcontrol;Username=devcontrol;Password=secret"
            })
            .Build();

        var connectionString = DatabaseConfiguration.GetConnectionString(configuration);

        Assert.Contains("Host=db", connectionString);
        Assert.Contains("Database=devcontrol", connectionString);
    }

    [Fact]
    public void GetConnectionString_BuildsFromDevControlPostgresVariables()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["POSTGRES_HOST"] = "postgres",
                ["POSTGRES_PORT"] = "5433",
                ["POSTGRES_DATABASE"] = "devcontrol",
                ["POSTGRES_USERNAME"] = "devcontrol",
                ["POSTGRES_PASSWORD"] = "secret"
            })
            .Build();

        var connectionString = DatabaseConfiguration.GetConnectionString(configuration);

        Assert.Contains("Host=postgres", connectionString);
        Assert.Contains("Port=5433", connectionString);
        Assert.Contains("Username=devcontrol", connectionString);
    }

    [Fact]
    public void GetConnectionString_ThrowsWhenConfigurationIsIncomplete()
    {
        var configuration = new ConfigurationBuilder().Build();

        var exception = Assert.Throws<InvalidOperationException>(() =>
        {
            _ = DatabaseConfiguration.GetConnectionString(configuration);
        });

        Assert.Contains("ConnectionStrings:DevControl", exception.Message);
    }
}
