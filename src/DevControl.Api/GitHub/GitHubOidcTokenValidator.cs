using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace DevControl.Api.GitHub;

public sealed class GitHubOidcTokenValidator(HttpClient httpClient, GitHubAppOptions options, TimeProvider timeProvider) : IGitHubOidcTokenValidator
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private JwksCache? jwksCache;

    public async Task<GitHubOidcClaims?> ValidateAsync(string token, string expectedAudience, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(token) || string.IsNullOrWhiteSpace(expectedAudience))
        {
            return null;
        }

        var parts = token.Split('.');
        if (parts.Length != 3)
        {
            return null;
        }

        using var headerDocument = JsonDocument.Parse(Base64UrlDecode(parts[0]));
        using var payloadDocument = JsonDocument.Parse(Base64UrlDecode(parts[1]));
        var header = headerDocument.RootElement;
        var payload = payloadDocument.RootElement;

        if (!string.Equals(ReadString(header, "alg"), "RS256", StringComparison.Ordinal))
        {
            return null;
        }

        var keyId = ReadString(header, "kid");
        if (string.IsNullOrWhiteSpace(keyId))
        {
            return null;
        }

        if (!await VerifySignatureAsync(keyId, $"{parts[0]}.{parts[1]}", parts[2], cancellationToken))
        {
            return null;
        }

        if (!string.Equals(ReadString(payload, "iss"), options.OidcIssuer, StringComparison.Ordinal))
        {
            return null;
        }

        if (!AudienceMatches(payload, expectedAudience))
        {
            return null;
        }

        var now = timeProvider.GetUtcNow().ToUnixTimeSeconds();
        if (TryReadLong(payload, "nbf", out var nbf) && now < nbf)
        {
            return null;
        }

        if (!TryReadLong(payload, "exp", out var exp) || now > exp)
        {
            return null;
        }

        var repository = ReadString(payload, "repository");
        var runId = ReadString(payload, "run_id");
        if (string.IsNullOrWhiteSpace(repository) || string.IsNullOrWhiteSpace(runId))
        {
            return null;
        }

        return new GitHubOidcClaims(
            repository,
            ReadString(payload, "ref"),
            ReadString(payload, "workflow_ref"),
            ReadString(payload, "workflow_sha"),
            runId,
            ReadString(payload, "actor"),
            ReadString(payload, "event_name"));
    }

    private async Task<bool> VerifySignatureAsync(string keyId, string signingInput, string encodedSignature, CancellationToken cancellationToken)
    {
        var keys = await GetKeysAsync(cancellationToken);
        var key = keys.Keys.FirstOrDefault(candidate => string.Equals(candidate.KeyId, keyId, StringComparison.Ordinal));
        if (key is null)
        {
            jwksCache = null;
            keys = await GetKeysAsync(cancellationToken);
            key = keys.Keys.FirstOrDefault(candidate => string.Equals(candidate.KeyId, keyId, StringComparison.Ordinal));
            if (key is null)
            {
                return false;
            }
        }

        using var rsa = RSA.Create();
        rsa.ImportParameters(new RSAParameters
        {
            Modulus = Base64UrlDecode(key.Modulus),
            Exponent = Base64UrlDecode(key.Exponent)
        });
        return rsa.VerifyData(
            Encoding.ASCII.GetBytes(signingInput),
            Base64UrlDecode(encodedSignature),
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);
    }

    private async Task<JwksCache> GetKeysAsync(CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        if (jwksCache is not null && jwksCache.ExpiresAt > now)
        {
            return jwksCache;
        }

        using var configResponse = await httpClient.GetAsync($"{options.OidcIssuer.TrimEnd('/')}/.well-known/openid-configuration", cancellationToken);
        configResponse.EnsureSuccessStatusCode();
        using var configDocument = JsonDocument.Parse(await configResponse.Content.ReadAsStringAsync(cancellationToken));
        var jwksUri = configDocument.RootElement.GetProperty("jwks_uri").GetString() ?? throw new InvalidOperationException("OIDC configuration did not include jwks_uri.");

        using var keysResponse = await httpClient.GetAsync(jwksUri, cancellationToken);
        keysResponse.EnsureSuccessStatusCode();
        using var keysDocument = JsonDocument.Parse(await keysResponse.Content.ReadAsStringAsync(cancellationToken));
        var keys = keysDocument.RootElement.GetProperty("keys")
            .EnumerateArray()
            .Where(key => string.Equals(ReadString(key, "kty"), "RSA", StringComparison.Ordinal) &&
                          string.Equals(ReadString(key, "use"), "sig", StringComparison.Ordinal))
            .Select(key => new Jwk(ReadString(key, "kid"), ReadString(key, "n"), ReadString(key, "e")))
            .ToArray();

        jwksCache = new JwksCache(keys, now.AddHours(6));
        return jwksCache;
    }

    private static bool AudienceMatches(JsonElement payload, string expectedAudience)
    {
        if (!payload.TryGetProperty("aud", out var aud))
        {
            return false;
        }

        if (aud.ValueKind == JsonValueKind.String)
        {
            return string.Equals(aud.GetString(), expectedAudience, StringComparison.Ordinal);
        }

        if (aud.ValueKind == JsonValueKind.Array)
        {
            return aud.EnumerateArray().Any(value => string.Equals(value.GetString(), expectedAudience, StringComparison.Ordinal));
        }

        return false;
    }

    private static string ReadString(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.String
            ? property.GetString() ?? string.Empty
            : string.Empty;
    }

    private static bool TryReadLong(JsonElement element, string propertyName, out long value)
    {
        value = 0;
        return element.TryGetProperty(propertyName, out var property) && property.TryGetInt64(out value);
    }

    private static byte[] Base64UrlDecode(string value)
    {
        var padded = value.Replace('-', '+').Replace('_', '/');
        padded = padded.PadRight(padded.Length + ((4 - padded.Length % 4) % 4), '=');
        return Convert.FromBase64String(padded);
    }

    private sealed record JwksCache(IReadOnlyList<Jwk> Keys, DateTimeOffset ExpiresAt);

    private sealed record Jwk(string KeyId, string Modulus, string Exponent);
}
