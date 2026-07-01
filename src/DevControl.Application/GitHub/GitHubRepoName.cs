using System.Text.RegularExpressions;

namespace DevControl.Application.GitHub;

public sealed record GitHubRepoName(string Owner, string Name)
{
    public string FullName => $"{Owner}/{Name}";

    public string NormalizedFullName => $"{Owner.ToLowerInvariant()}/{Name.ToLowerInvariant()}";
}

public static partial class GitHubRepoNameParser
{
    public static bool TryParse(string? value, out GitHubRepoName repo)
    {
        var raw = (value ?? string.Empty).Trim();
        if (raw.StartsWith("https://github.com/", StringComparison.OrdinalIgnoreCase))
        {
            raw = raw["https://github.com/".Length..].TrimEnd('/');
        }

        if (raw.EndsWith(".git", StringComparison.OrdinalIgnoreCase))
        {
            raw = raw[..^4];
        }

        var match = RepoRegex().Match(raw);
        if (!match.Success)
        {
            repo = new GitHubRepoName(string.Empty, string.Empty);
            return false;
        }

        repo = new GitHubRepoName(match.Groups["owner"].Value, match.Groups["repo"].Value);
        return true;
    }

    [GeneratedRegex("^(?<owner>[A-Za-z0-9_.-]+)/(?<repo>[A-Za-z0-9_.-]+)$")]
    private static partial Regex RepoRegex();
}
