namespace DevControl.Domain.Entities;

public sealed class ApiKeyUsageDaily
{
    public Guid Id { get; private set; }

    public Guid ApiKeyId { get; private set; }

    public Guid OrganizationId { get; private set; }

    public Guid ProjectId { get; private set; }

    public Guid EnvironmentId { get; private set; }

    public DateOnly Day { get; private set; }

    public string Endpoint { get; private set; } = string.Empty;

    public long RequestCount { get; private set; }

    public long FailureCount { get; private set; }

    public long RateLimitHitCount { get; private set; }

    public long TotalLatencyMilliseconds { get; private set; }

    public long LatencySampleCount { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    private ApiKeyUsageDaily()
    {
    }

    public ApiKeyUsageDaily(
        Guid apiKeyId,
        Guid organizationId,
        Guid projectId,
        Guid environmentId,
        DateOnly day,
        string endpoint,
        DateTimeOffset now)
    {
        Id = Guid.NewGuid();
        ApiKeyId = apiKeyId;
        OrganizationId = organizationId;
        ProjectId = projectId;
        EnvironmentId = environmentId;
        Day = day;
        Endpoint = Require(endpoint, nameof(endpoint));
        UpdatedAt = now;
    }

    public void RecordUsage(bool failed, int? latencyMilliseconds, bool rateLimitHit, DateTimeOffset now)
    {
        RequestCount++;

        if (failed)
        {
            FailureCount++;
        }

        if (rateLimitHit)
        {
            RateLimitHitCount++;
        }

        if (latencyMilliseconds is >= 0)
        {
            TotalLatencyMilliseconds += latencyMilliseconds.Value;
            LatencySampleCount++;
        }

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
