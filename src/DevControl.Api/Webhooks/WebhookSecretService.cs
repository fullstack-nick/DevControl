using System.Security.Cryptography;
using Microsoft.AspNetCore.DataProtection;

namespace DevControl.Api.Webhooks;

public sealed class WebhookSecretService(IDataProtectionProvider dataProtectionProvider)
{
    private const string Purpose = "DevControl.WebhookSecrets.v1";
    private readonly IDataProtector protector = dataProtectionProvider.CreateProtector(Purpose);

    public WebhookSecret CreateSecret()
    {
        Span<byte> bytes = stackalloc byte[32];
        RandomNumberGenerator.Fill(bytes);
        var secret = "dwhsec_" + Base64UrlEncode(bytes);
        return new WebhookSecret(secret[..16], secret, protector.Protect(secret));
    }

    public string Unprotect(string protectedSecret)
    {
        return protector.Unprotect(protectedSecret);
    }

    private static string Base64UrlEncode(ReadOnlySpan<byte> bytes)
    {
        return Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }
}

public sealed record WebhookSecret(string Prefix, string Secret, string ProtectedSecret);
