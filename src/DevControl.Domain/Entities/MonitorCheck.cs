using DevControl.Domain.Enums;

namespace DevControl.Domain.Entities;

public sealed class MonitorCheck
{
    public Guid Id { get; private set; }

    public Guid UptimeMonitorId { get; private set; }

    public Guid OrganizationId { get; private set; }

    public Guid ProjectId { get; private set; }

    public Guid EnvironmentId { get; private set; }

    public Guid? LiveAppId { get; private set; }

    public MonitorStatus Status { get; private set; }

    public bool Succeeded { get; private set; }

    public int? StatusCode { get; private set; }

    public string ResultKind { get; private set; } = string.Empty;

    public long DurationMilliseconds { get; private set; }

    public string Error { get; private set; } = string.Empty;

    public string ResponsePreview { get; private set; } = string.Empty;

    public bool ResponseTruncated { get; private set; }

    public DateTimeOffset CheckedAt { get; private set; }

    private MonitorCheck()
    {
    }

    public MonitorCheck(
        UptimeMonitor monitor,
        MonitorStatus status,
        bool succeeded,
        int? statusCode,
        string resultKind,
        long durationMilliseconds,
        string? error,
        string? responsePreview,
        bool responseTruncated,
        DateTimeOffset checkedAt)
    {
        Id = Guid.NewGuid();
        UptimeMonitorId = monitor.Id;
        OrganizationId = monitor.OrganizationId;
        ProjectId = monitor.ProjectId;
        EnvironmentId = monitor.EnvironmentId;
        LiveAppId = monitor.LiveAppId;
        Status = status;
        Succeeded = succeeded;
        StatusCode = statusCode;
        ResultKind = string.IsNullOrWhiteSpace(resultKind) ? status.ToString() : resultKind.Trim();
        DurationMilliseconds = durationMilliseconds;
        Error = string.IsNullOrWhiteSpace(error) ? string.Empty : error.Trim();
        ResponsePreview = string.IsNullOrWhiteSpace(responsePreview) ? string.Empty : responsePreview.Trim();
        ResponseTruncated = responseTruncated;
        CheckedAt = checkedAt;
    }
}
