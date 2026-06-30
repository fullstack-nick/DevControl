using System.Security.Cryptography;
using System.Text;

namespace DevControl.Application.Security;

public sealed record ApiKeySecret(string Secret, string Prefix, string Hash);

public sealed class ApiKeySecretService
{
    private const string KeyPrefix = "dck_";

    public ApiKeySecret CreateKey()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        var body = ToBase64Url(bytes);
        var secret = $"{KeyPrefix}{body}";
        return new ApiKeySecret(secret, secret[..16], HashKey(secret));
    }

    public string HashKey(string key)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(key.Trim()));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private static string ToBase64Url(byte[] bytes)
    {
        return Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }
}
