using System.Text.RegularExpressions;

namespace DevControl.Application.GitHub;

public static class DevControlSetupActionReference
{
    public const string Default = "fullstack-nick/DevControl/.github/actions/setup-devcontrol@main";

    private static readonly Regex ActionReferencePattern = new(
        "^[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+(?:/[A-Za-z0-9_.-]+)*@[-A-Za-z0-9_./]+$",
        RegexOptions.CultureInvariant);

    public static string Normalize(string? value)
    {
        var normalized = string.IsNullOrWhiteSpace(value)
            ? Default
            : value.Trim();

        if (!ActionReferencePattern.IsMatch(normalized) || normalized.Contains("..", StringComparison.Ordinal))
        {
            throw new ArgumentException("DevControl setup action reference must be a GitHub action reference such as owner/repo/path@ref.", nameof(value));
        }

        return normalized;
    }
}
