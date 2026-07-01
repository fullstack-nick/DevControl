namespace DevControl.Api.GitHub;

public sealed class GitHubAppOptions
{
    public string ApiBaseUrl { get; init; } = "https://api.github.com";

    public string AppId { get; init; } = string.Empty;

    public string PrivateKey { get; init; } = string.Empty;

    public string OidcIssuer { get; init; } = "https://token.actions.githubusercontent.com";

    public bool IsConfigured => long.TryParse(AppId, out _) && !string.IsNullOrWhiteSpace(PrivateKey);

    public static GitHubAppOptions FromConfiguration(IConfiguration configuration)
    {
        return new GitHubAppOptions
        {
            ApiBaseUrl = configuration["GITHUB_APP_API_BASE_URL"] ?? "https://api.github.com",
            AppId = configuration["GITHUB_APP_ID"] ?? string.Empty,
            PrivateKey = NormalizePrivateKey(configuration["GITHUB_APP_PRIVATE_KEY"] ?? string.Empty),
            OidcIssuer = configuration["GITHUB_OIDC_ISSUER"] ?? "https://token.actions.githubusercontent.com"
        };
    }

    private static string NormalizePrivateKey(string value)
    {
        return value.Replace("\\n", "\n", StringComparison.Ordinal).Trim();
    }
}
