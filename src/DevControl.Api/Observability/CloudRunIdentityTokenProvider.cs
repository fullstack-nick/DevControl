using System.Text.Json;

namespace DevControl.Api.Observability;

public sealed class CloudRunIdentityTokenProvider(HttpClient httpClient, TimeProvider timeProvider)
{
    private readonly SemaphoreSlim semaphore = new(1, 1);
    private string? cachedAudience;
    private string? cachedToken;
    private DateTimeOffset expiresAt;

    public async Task<string> GetTokenAsync(Uri audience, CancellationToken cancellationToken)
    {
        var audienceValue = audience.GetLeftPart(UriPartial.Authority);
        var now = timeProvider.GetUtcNow();
        if (cachedToken is not null &&
            string.Equals(cachedAudience, audienceValue, StringComparison.Ordinal) &&
            expiresAt > now.AddMinutes(5))
        {
            return cachedToken;
        }

        await semaphore.WaitAsync(cancellationToken);
        try
        {
            now = timeProvider.GetUtcNow();
            if (cachedToken is not null &&
                string.Equals(cachedAudience, audienceValue, StringComparison.Ordinal) &&
                expiresAt > now.AddMinutes(5))
            {
                return cachedToken;
            }

            using var request = new HttpRequestMessage(
                HttpMethod.Get,
                $"computeMetadata/v1/instance/service-accounts/default/identity?audience={Uri.EscapeDataString(audienceValue)}");
            request.Headers.TryAddWithoutValidation("Metadata-Flavor", "Google");

            var token = await httpClient.SendAsync(request, cancellationToken);
            token.EnsureSuccessStatusCode();

            cachedAudience = audienceValue;
            cachedToken = (await token.Content.ReadAsStringAsync(cancellationToken)).Trim();
            expiresAt = ReadJwtExpiry(cachedToken) ?? now.AddMinutes(50);
            return cachedToken;
        }
        finally
        {
            semaphore.Release();
        }
    }

    private static DateTimeOffset? ReadJwtExpiry(string token)
    {
        var parts = token.Split('.');
        if (parts.Length < 2)
        {
            return null;
        }

        try
        {
            var payload = parts[1]
                .Replace('-', '+')
                .Replace('_', '/');
            payload = payload.PadRight(payload.Length + (4 - payload.Length % 4) % 4, '=');
            var bytes = Convert.FromBase64String(payload);
            using var document = JsonDocument.Parse(bytes);
            return document.RootElement.TryGetProperty("exp", out var exp)
                ? DateTimeOffset.FromUnixTimeSeconds(exp.GetInt64())
                : null;
        }
        catch (FormatException)
        {
            return null;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
