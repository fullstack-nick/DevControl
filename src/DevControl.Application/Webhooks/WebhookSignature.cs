using System.Security.Cryptography;
using System.Text;

namespace DevControl.Application.Webhooks;

public static class WebhookSignature
{
    public static string Sign(string secret, DateTimeOffset timestamp, Guid deliveryId, string body)
    {
        var payload = $"{timestamp.ToUnixTimeSeconds()}.{deliveryId}.{body}";
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        return "sha256=" + Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(payload))).ToLowerInvariant();
    }
}
