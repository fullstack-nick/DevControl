using System.Security.Cryptography;
using System.Text;

namespace DevControl.Application.Security;

public static class OperatorSecretValidator
{
    public const string HeaderName = "X-DevControl-Operator-Secret";

    public static bool IsValid(string? configuredSecret, string? providedSecret)
    {
        if (string.IsNullOrWhiteSpace(configuredSecret) || string.IsNullOrWhiteSpace(providedSecret))
        {
            return false;
        }

        var configuredHash = SHA256.HashData(Encoding.UTF8.GetBytes(configuredSecret));
        var providedHash = SHA256.HashData(Encoding.UTF8.GetBytes(providedSecret));
        return CryptographicOperations.FixedTimeEquals(configuredHash, providedHash);
    }
}
