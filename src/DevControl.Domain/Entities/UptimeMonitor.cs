using DevControl.Domain.Enums;

namespace DevControl.Domain.Entities;

public sealed class UptimeMonitor
{
    public const int DefaultIntervalSeconds = 300;
    public const int DefaultTimeoutSeconds = 5;
    public const int DefaultSlowThresholdMilliseconds = 2000;
    public const int DefaultFailureThreshold = 1;
    public const int DefaultRecoveryThreshold = 1;

    public Guid Id { get; private set; }

    public Guid OrganizationId { get; private set; }

    public Guid ProjectId { get; private set; }

    public Guid EnvironmentId { get; private set; }

    public Guid? LiveAppId { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public string Url { get; private set; } = string.Empty;

    public bool IsManagedFromLiveApp { get; private set; }

    public bool IsPaused { get; private set; }

    public MonitorStatus CurrentStatus { get; private set; }

    public int IntervalSeconds { get; private set; }

    public int TimeoutSeconds { get; private set; }

    public int SlowThresholdMilliseconds { get; private set; }

    public int FailureThreshold { get; private set; }

    public int RecoveryThreshold { get; private set; }

    public int ConsecutiveFailures { get; private set; }

    public int ConsecutiveRecoveries { get; private set; }

    public DateTimeOffset NextCheckAt { get; private set; }

    public DateTimeOffset? LastCheckedAt { get; private set; }

    public DateTimeOffset? LastSuccessAt { get; private set; }

    public DateTimeOffset? LastFailureAt { get; private set; }

    public Guid? CreatedByUserId { get; private set; }

    public Guid? UpdatedByUserId { get; private set; }

    public Guid? PausedByUserId { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public DateTimeOffset? PausedAt { get; private set; }

    public string? ProcessingLeaseId { get; private set; }

    public DateTimeOffset? ProcessingLeaseExpiresAt { get; private set; }

    private UptimeMonitor()
    {
    }

    public UptimeMonitor(
        Guid organizationId,
        Guid projectId,
        Guid environmentId,
        Guid? liveAppId,
        string name,
        string url,
        bool isManagedFromLiveApp,
        Guid? createdByUserId,
        DateTimeOffset now)
    {
        Id = Guid.NewGuid();
        OrganizationId = organizationId;
        ProjectId = projectId;
        EnvironmentId = environmentId;
        LiveAppId = liveAppId;
        IsManagedFromLiveApp = isManagedFromLiveApp;
        CurrentStatus = MonitorStatus.Unknown;
        IntervalSeconds = DefaultIntervalSeconds;
        TimeoutSeconds = DefaultTimeoutSeconds;
        SlowThresholdMilliseconds = DefaultSlowThresholdMilliseconds;
        FailureThreshold = DefaultFailureThreshold;
        RecoveryThreshold = DefaultRecoveryThreshold;
        CreatedByUserId = createdByUserId;
        CreatedAt = now;
        NextCheckAt = now;
        UpdateDefinition(name, url, createdByUserId, now);
    }

    public void UpdateDefinition(string name, string url, Guid? updatedByUserId, DateTimeOffset now)
    {
        Name = Require(name, nameof(name), 160);
        Url = Require(url, nameof(url), 1000);
        UpdatedByUserId = updatedByUserId;
        UpdatedAt = now;
    }

    public void UpdateSettings(
        string name,
        string url,
        int intervalSeconds,
        int timeoutSeconds,
        int slowThresholdMilliseconds,
        int failureThreshold,
        int recoveryThreshold,
        Guid updatedByUserId,
        DateTimeOffset now)
    {
        if (intervalSeconds is < 60 or > 86400)
        {
            throw new ArgumentOutOfRangeException(nameof(intervalSeconds), "Monitor interval must be between 60 and 86400 seconds.");
        }

        if (timeoutSeconds is < 1 or > 30)
        {
            throw new ArgumentOutOfRangeException(nameof(timeoutSeconds), "Monitor timeout must be between 1 and 30 seconds.");
        }

        if (slowThresholdMilliseconds is < 100 or > 30000)
        {
            throw new ArgumentOutOfRangeException(nameof(slowThresholdMilliseconds), "Slow threshold must be between 100 and 30000 milliseconds.");
        }

        if (failureThreshold is < 1 or > 10)
        {
            throw new ArgumentOutOfRangeException(nameof(failureThreshold), "Failure threshold must be between 1 and 10.");
        }

        if (recoveryThreshold is < 1 or > 10)
        {
            throw new ArgumentOutOfRangeException(nameof(recoveryThreshold), "Recovery threshold must be between 1 and 10.");
        }

        UpdateDefinition(name, url, updatedByUserId, now);
        IntervalSeconds = intervalSeconds;
        TimeoutSeconds = timeoutSeconds;
        SlowThresholdMilliseconds = slowThresholdMilliseconds;
        FailureThreshold = failureThreshold;
        RecoveryThreshold = recoveryThreshold;
        NextCheckAt = NextCheckAt < now ? now : NextCheckAt;
    }

    public void Pause(Guid pausedByUserId, DateTimeOffset now)
    {
        IsPaused = true;
        CurrentStatus = MonitorStatus.Paused;
        PausedByUserId = pausedByUserId;
        PausedAt = now;
        ProcessingLeaseId = null;
        ProcessingLeaseExpiresAt = null;
        UpdatedByUserId = pausedByUserId;
        UpdatedAt = now;
    }

    public void Resume(Guid resumedByUserId, DateTimeOffset now)
    {
        IsPaused = false;
        CurrentStatus = MonitorStatus.Unknown;
        PausedByUserId = null;
        PausedAt = null;
        ConsecutiveFailures = 0;
        ConsecutiveRecoveries = 0;
        NextCheckAt = now;
        UpdatedByUserId = resumedByUserId;
        UpdatedAt = now;
    }

    public void Lease(string leaseId, DateTimeOffset leaseExpiresAt, DateTimeOffset now)
    {
        ProcessingLeaseId = string.IsNullOrWhiteSpace(leaseId) ? throw new ArgumentException("Lease id is required.", nameof(leaseId)) : leaseId.Trim();
        ProcessingLeaseExpiresAt = leaseExpiresAt;
        UpdatedAt = now;
    }

    public void RecordCheck(MonitorStatus status, DateTimeOffset now)
    {
        LastCheckedAt = now;
        NextCheckAt = now.AddSeconds(IntervalSeconds);
        CurrentStatus = IsPaused ? MonitorStatus.Paused : status;
        ProcessingLeaseId = null;
        ProcessingLeaseExpiresAt = null;

        if (status is MonitorStatus.Up or MonitorStatus.Slow)
        {
            LastSuccessAt = now;
            ConsecutiveRecoveries++;
            ConsecutiveFailures = 0;
        }
        else if (status == MonitorStatus.Down)
        {
            LastFailureAt = now;
            ConsecutiveFailures++;
            ConsecutiveRecoveries = 0;
        }

        UpdatedAt = now;
    }

    private static string Require(string value, string paramName, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException($"{paramName} is required.", paramName);
        }

        var trimmed = value.Trim();
        if (trimmed.Length > maxLength)
        {
            throw new ArgumentException($"{paramName} cannot exceed {maxLength} characters.", paramName);
        }

        return trimmed;
    }
}
