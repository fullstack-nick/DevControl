namespace DevControl.Domain.Entities;

public sealed class ApiKey
{
    public Guid Id { get; private set; }

    public Guid OrganizationId { get; private set; }

    public Guid ProjectId { get; private set; }

    public Guid EnvironmentId { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public string KeyPrefix { get; private set; } = string.Empty;

    public string KeyHash { get; private set; } = string.Empty;

    public string ScopesJson { get; private set; } = "[]";

    public int RateLimitPerMinute { get; private set; }

    public Guid CreatedByUserId { get; private set; }

    public Guid? RevokedByUserId { get; private set; }

    public Guid? RotatedFromApiKeyId { get; private set; }

    public Guid? RotatedToApiKeyId { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset? LastUsedAt { get; private set; }

    public DateTimeOffset? RevokedAt { get; private set; }

    public DateTimeOffset? RotatedAt { get; private set; }

    public long TotalRequestCount { get; private set; }

    public long FailureCount { get; private set; }

    public long RateLimitHitCount { get; private set; }

    public long TotalLatencyMilliseconds { get; private set; }

    public long LatencySampleCount { get; private set; }

    public bool IsRevoked => RevokedAt is not null;

    private ApiKey()
    {
    }

    public ApiKey(
        Guid organizationId,
        Guid projectId,
        Guid environmentId,
        string name,
        string keyPrefix,
        string keyHash,
        string scopesJson,
        int rateLimitPerMinute,
        Guid createdByUserId,
        DateTimeOffset now,
        Guid? rotatedFromApiKeyId = null)
    {
        if (rateLimitPerMinute <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(rateLimitPerMinute), "Rate limit must be positive.");
        }

        Id = Guid.NewGuid();
        OrganizationId = organizationId;
        ProjectId = projectId;
        EnvironmentId = environmentId;
        CreatedByUserId = createdByUserId;
        CreatedAt = now;
        Name = Require(name, nameof(name));
        KeyPrefix = Require(keyPrefix, nameof(keyPrefix));
        KeyHash = Require(keyHash, nameof(keyHash));
        ScopesJson = string.IsNullOrWhiteSpace(scopesJson) ? "[]" : scopesJson.Trim();
        RateLimitPerMinute = rateLimitPerMinute;
        RotatedFromApiKeyId = rotatedFromApiKeyId;
    }

    public void Revoke(Guid revokedByUserId, DateTimeOffset now)
    {
        if (RevokedAt is not null)
        {
            return;
        }

        RevokedByUserId = revokedByUserId;
        RevokedAt = now;
    }

    public void MarkRotated(Guid revokedByUserId, Guid rotatedToApiKeyId, DateTimeOffset now)
    {
        RotatedToApiKeyId = rotatedToApiKeyId;
        RotatedAt = now;
        Revoke(revokedByUserId, now);
    }

    public void RecordUsage(bool failed, int? latencyMilliseconds, bool rateLimitHit, DateTimeOffset now)
    {
        LastUsedAt = now;
        TotalRequestCount++;

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
