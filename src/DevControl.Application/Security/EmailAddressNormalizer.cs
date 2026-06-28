using System.Globalization;

namespace DevControl.Application.Security;

public static class EmailAddressNormalizer
{
    public static string Normalize(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            throw new ArgumentException("Email is required.", nameof(email));
        }

        var trimmed = email.Trim();
        var atIndex = trimmed.IndexOf('@', StringComparison.Ordinal);
        if (atIndex <= 0 || atIndex == trimmed.Length - 1)
        {
            throw new ArgumentException("Email must contain a local part and domain.", nameof(email));
        }

        return trimmed.ToUpperInvariant();
    }

    public static string Display(string email) => email.Trim().ToLower(CultureInfo.InvariantCulture);
}
