namespace DevControl.Domain.Entities;

public sealed class ApiKeyRateLimitWindow
{
    public Guid Id { get; private set; }

    public Guid ApiKeyId { get; private set; }

    public string Endpoint { get; private set; } = string.Empty;

    public DateTimeOffset WindowStart { get; private set; }

    public int RequestCount { get; private set; }

    public int RateLimitHitCount { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    private ApiKeyRateLimitWindow()
    {
    }

    public ApiKeyRateLimitWindow(Guid apiKeyId, string endpoint, DateTimeOffset windowStart, DateTimeOffset now)
    {
        Id = Guid.NewGuid();
        ApiKeyId = apiKeyId;
        Endpoint = Require(endpoint, nameof(endpoint));
        WindowStart = windowStart;
        CreatedAt = now;
        UpdatedAt = now;
    }

    public void Increment(DateTimeOffset now)
    {
        RequestCount++;
        UpdatedAt = now;
    }

    public void MarkRateLimitHit(DateTimeOffset now)
    {
        RateLimitHitCount++;
        UpdatedAt = now;
    }

    private static string Require(string value, string paramName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException($"{paramName} is required.", paramName);
        }

        return value.Trim();
    }
}
