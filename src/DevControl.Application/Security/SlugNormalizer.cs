using System.Text;
using System.Text.RegularExpressions;

namespace DevControl.Application.Security;

public static partial class SlugNormalizer
{
    public static string Normalize(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Slug source is required.", nameof(value));
        }

        var builder = new StringBuilder(value.Trim().Length);
        foreach (var character in value.Trim().ToLowerInvariant())
        {
            if (character is >= 'a' and <= 'z' or >= '0' and <= '9')
            {
                builder.Append(character);
            }
            else if (character is ' ' or '_' or '-' or '.')
            {
                builder.Append('-');
            }
        }

        var collapsed = HyphenRun().Replace(builder.ToString(), "-").Trim('-');
        if (string.IsNullOrWhiteSpace(collapsed))
        {
            throw new ArgumentException("Slug must contain at least one letter or number.", nameof(value));
        }

        return collapsed.Length <= 80 ? collapsed : collapsed[..80].Trim('-');
    }

    [GeneratedRegex("-+")]
    private static partial Regex HyphenRun();
}
