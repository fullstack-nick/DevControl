namespace DevControl.Domain.Entities;

public sealed class AuditLog
{
    public Guid Id { get; private set; }

    public Guid OrganizationId { get; private set; }

    public Guid? ProjectId { get; private set; }

    public Guid? EnvironmentId { get; private set; }

    public Guid? ActorUserId { get; private set; }

    public string ActorEmail { get; private set; } = string.Empty;

    public string Action { get; private set; } = string.Empty;

    public string Outcome { get; private set; } = string.Empty;

    public string TargetType { get; private set; } = string.Empty;

    public string? TargetId { get; private set; }

    public string Message { get; private set; } = string.Empty;

    public string MetadataJson { get; private set; } = "{}";

    public string IpAddress { get; private set; } = string.Empty;

    public string UserAgent { get; private set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; private set; }

    private AuditLog()
    {
    }

    public AuditLog(
        Guid organizationId,
        Guid? projectId,
        Guid? environmentId,
        Guid? actorUserId,
        string actorEmail,
        string action,
        string outcome,
        string targetType,
        string? targetId,
        string message,
        string metadataJson,
        string ipAddress,
        string userAgent,
        DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(action))
        {
            throw new ArgumentException("Audit action is required.", nameof(action));
        }

        if (string.IsNullOrWhiteSpace(outcome))
        {
            throw new ArgumentException("Audit outcome is required.", nameof(outcome));
        }

        if (string.IsNullOrWhiteSpace(targetType))
        {
            throw new ArgumentException("Audit target type is required.", nameof(targetType));
        }

        Id = Guid.NewGuid();
        OrganizationId = organizationId;
        ProjectId = projectId;
        EnvironmentId = environmentId;
        ActorUserId = actorUserId;
        ActorEmail = actorEmail.Trim();
        Action = action.Trim();
        Outcome = outcome.Trim();
        TargetType = targetType.Trim();
        TargetId = string.IsNullOrWhiteSpace(targetId) ? null : targetId.Trim();
        Message = message.Trim();
        MetadataJson = string.IsNullOrWhiteSpace(metadataJson) ? "{}" : metadataJson.Trim();
        IpAddress = ipAddress.Trim();
        UserAgent = userAgent.Trim();
        CreatedAt = now;
    }
}
