using System.Text.RegularExpressions;

namespace DevControl.Application.Security;

public static partial class FeatureFlagKeys
{
    private const int MaxLength = 120;

    public static bool TryNormalize(string? requestedKey, out string key, out string? error)
    {
        key = string.IsNullOrWhiteSpace(requestedKey) ? string.Empty : requestedKey.Trim().ToLowerInvariant();
        if (key.Length == 0)
        {
            error = "Flag key is required.";
            return false;
        }

        if (key.Length > MaxLength)
        {
            error = $"Flag key cannot exceed {MaxLength} characters.";
            return false;
        }

        if (!FlagKeyRegex().IsMatch(key))
        {
            error = "Flag key can contain lowercase letters, numbers, dots, underscores, and hyphens.";
            return false;
        }

        error = null;
        return true;
    }

    [GeneratedRegex("^[a-z0-9][a-z0-9._-]*$", RegexOptions.CultureInvariant)]
    private static partial Regex FlagKeyRegex();
}
