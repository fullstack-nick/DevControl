using System.Security.Cryptography;
using System.Text;

namespace DevControl.Api.Observability;

public sealed class MetricsAccessOptions
{
    public const string MissingProductionTokenMessage =
        "DEVCONTROL_METRICS_SCRAPE_TOKEN is required when DEVCONTROL_METRICS_ENABLED=true outside Development/Test.";

    private readonly byte[]? scrapeTokenHash;

    private MetricsAccessOptions(bool enabled, string? scrapeToken)
    {
        Enabled = enabled;
        ScrapeToken = string.IsNullOrWhiteSpace(scrapeToken) ? null : scrapeToken.Trim();
        scrapeTokenHash = ScrapeToken is null ? null : SHA256.HashData(Encoding.UTF8.GetBytes(ScrapeToken));
    }

    public bool Enabled { get; }

    public string? ScrapeToken { get; }

    public bool RequiresToken => scrapeTokenHash is not null;

    public static MetricsAccessOptions FromConfiguration(IConfiguration configuration, IHostEnvironment environment)
    {
        var enabled = bool.TryParse(configuration["METRICS_ENABLED"], out var parsed) && parsed;
        var scrapeToken = configuration["METRICS_SCRAPE_TOKEN"];
        var tokenMissing = string.IsNullOrWhiteSpace(scrapeToken);
        if (enabled && tokenMissing && !AllowsTokenlessMetrics(environment.EnvironmentName))
        {
            throw new InvalidOperationException(MissingProductionTokenMessage);
        }

        return new MetricsAccessOptions(enabled, scrapeToken);
    }

    public bool IsAuthorized(string? authorizationHeader)
    {
        if (!RequiresToken)
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(authorizationHeader) ||
            !authorizationHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var providedToken = authorizationHeader["Bearer ".Length..].Trim();
        if (providedToken.Length == 0)
        {
            return false;
        }

        var providedHash = SHA256.HashData(Encoding.UTF8.GetBytes(providedToken));
        return CryptographicOperations.FixedTimeEquals(providedHash, scrapeTokenHash);
    }

    private static bool AllowsTokenlessMetrics(string environmentName)
    {
        return string.Equals(environmentName, Environments.Development, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(environmentName, "Test", StringComparison.OrdinalIgnoreCase);
    }
}
