namespace DevControl.Domain.Entities;

public sealed class WebhookEvent
{
    public Guid Id { get; private set; }

    public Guid OrganizationId { get; private set; }

    public Guid ProjectId { get; private set; }

    public Guid EnvironmentId { get; private set; }

    public string EventType { get; private set; } = string.Empty;

    public string ResourceType { get; private set; } = string.Empty;

    public string? ResourceId { get; private set; }

    public Guid? ActorUserId { get; private set; }

    public string ActorEmail { get; private set; } = string.Empty;

    public string PayloadJson { get; private set; } = "{}";

    public DateTimeOffset OccurredAt { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    private WebhookEvent()
    {
    }

    public WebhookEvent(
        Guid organizationId,
        Guid projectId,
        Guid environmentId,
        string eventType,
        string resourceType,
        string? resourceId,
        Guid? actorUserId,
        string actorEmail,
        string payloadJson,
        DateTimeOffset occurredAt)
    {
        Id = Guid.NewGuid();
        OrganizationId = organizationId;
        ProjectId = projectId;
        EnvironmentId = environmentId;
        EventType = Require(eventType, nameof(eventType));
        ResourceType = Require(resourceType, nameof(resourceType));
        ResourceId = string.IsNullOrWhiteSpace(resourceId) ? null : resourceId.Trim();
        ActorUserId = actorUserId;
        ActorEmail = string.IsNullOrWhiteSpace(actorEmail) ? "system" : actorEmail.Trim();
        PayloadJson = string.IsNullOrWhiteSpace(payloadJson) ? "{}" : payloadJson.Trim();
        OccurredAt = occurredAt;
        CreatedAt = occurredAt;
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
