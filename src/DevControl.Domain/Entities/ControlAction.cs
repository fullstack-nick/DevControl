using DevControl.Domain.Enums;

namespace DevControl.Domain.Entities;

public sealed class ControlAction
{
    public Guid Id { get; private set; }

    public Guid OrganizationId { get; private set; }

    public Guid? ProjectId { get; private set; }

    public Guid? EnvironmentId { get; private set; }

    public string ActionType { get; private set; } = string.Empty;

    public ControlActionStatus Status { get; private set; }

    public Guid RequestedByUserId { get; private set; }

    public string TargetType { get; private set; } = string.Empty;

    public string? TargetId { get; private set; }

    public string RequestJson { get; private set; } = "{}";

    public string ResultJson { get; private set; } = "{}";

    public string? CorrelationId { get; private set; }

    public DateTimeOffset RequestedAt { get; private set; }

    public DateTimeOffset? StartedAt { get; private set; }

    public DateTimeOffset? CompletedAt { get; private set; }

    private ControlAction()
    {
    }

    public ControlAction(
        Guid organizationId,
        Guid? projectId,
        Guid? environmentId,
        string actionType,
        Guid requestedByUserId,
        string targetType,
        string? targetId,
        string requestJson,
        DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(actionType))
        {
            throw new ArgumentException("Control action type is required.", nameof(actionType));
        }

        if (string.IsNullOrWhiteSpace(targetType))
        {
            throw new ArgumentException("Control action target type is required.", nameof(targetType));
        }

        Id = Guid.NewGuid();
        OrganizationId = organizationId;
        ProjectId = projectId;
        EnvironmentId = environmentId;
        ActionType = actionType.Trim();
        Status = ControlActionStatus.Pending;
        RequestedByUserId = requestedByUserId;
        TargetType = targetType.Trim();
        TargetId = string.IsNullOrWhiteSpace(targetId) ? null : targetId.Trim();
        RequestJson = string.IsNullOrWhiteSpace(requestJson) ? "{}" : requestJson.Trim();
        RequestedAt = now;
    }

    public void MarkStarted(DateTimeOffset now)
    {
        Status = ControlActionStatus.InProgress;
        StartedAt = now;
    }

    public void MarkCompleted(ControlActionStatus status, string resultJson, string? correlationId, DateTimeOffset now)
    {
        Status = status;
        ResultJson = string.IsNullOrWhiteSpace(resultJson) ? "{}" : resultJson.Trim();
        CorrelationId = string.IsNullOrWhiteSpace(correlationId) ? null : correlationId.Trim();
        CompletedAt = now;
    }
}
