using Microsoft.Extensions.Configuration;
using Npgsql;

namespace DevControl.Infrastructure.Database;

public static class DatabaseConfiguration
{
    public static string GetConnectionString(IConfiguration configuration)
    {
        var configuredConnectionString = configuration.GetConnectionString("DevControl");
        if (!string.IsNullOrWhiteSpace(configuredConnectionString))
        {
            return configuredConnectionString;
        }

        var host = configuration["POSTGRES_HOST"];
        var database = configuration["POSTGRES_DATABASE"];
        var username = configuration["POSTGRES_USERNAME"];
        var password = configuration["POSTGRES_PASSWORD"];
        var portText = configuration["POSTGRES_PORT"];

        if (string.IsNullOrWhiteSpace(host) ||
            string.IsNullOrWhiteSpace(database) ||
            string.IsNullOrWhiteSpace(username) ||
            string.IsNullOrWhiteSpace(password))
        {
            throw new InvalidOperationException(
                "Configure either ConnectionStrings:DevControl or DEVCONTROL_POSTGRES_HOST, DEVCONTROL_POSTGRES_DATABASE, DEVCONTROL_POSTGRES_USERNAME, and DEVCONTROL_POSTGRES_PASSWORD.");
        }

        var builder = new NpgsqlConnectionStringBuilder
        {
            Host = host,
            Database = database,
            Username = username,
            Password = password,
            Port = int.TryParse(portText, out var port) ? port : 5432,
            IncludeErrorDetail = false,
            Pooling = true
        };

        return builder.ConnectionString;
    }
}

