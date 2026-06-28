using System.Security.Cryptography;
using System.Text;

namespace DevControl.Application.Security;

public sealed record RegistrationTokenSecret(string Secret, string Prefix, string Hash);

public sealed class RegistrationTokenService
{
    private const string TokenPrefix = "dcr_";

    public RegistrationTokenSecret CreateToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        var body = ToBase64Url(bytes);
        var secret = $"{TokenPrefix}{body}";
        return new RegistrationTokenSecret(secret, secret[..16], HashToken(secret));
    }

    public string HashToken(string token)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(token.Trim()));
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
