using DevControl.Domain.Enums;

namespace DevControl.Domain.Entities;

public sealed class IncidentUpdate
{
    public Guid Id { get; private set; }

    public Guid IncidentId { get; private set; }

    public Guid OrganizationId { get; private set; }

    public Guid ProjectId { get; private set; }

    public Guid EnvironmentId { get; private set; }

    public IncidentStatus Status { get; private set; }

    public IncidentUpdateVisibility Visibility { get; private set; }

    public string Message { get; private set; } = string.Empty;

    public Guid? CreatedByUserId { get; private set; }

    public string CreatedByEmail { get; private set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; private set; }

    private IncidentUpdate()
    {
    }

    public IncidentUpdate(
        Incident incident,
        IncidentStatus status,
        IncidentUpdateVisibility visibility,
        string message,
        Guid? createdByUserId,
        string? createdByEmail,
        DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            throw new ArgumentException("Incident update message is required.", nameof(message));
        }

        Id = Guid.NewGuid();
        IncidentId = incident.Id;
        OrganizationId = incident.OrganizationId;
        ProjectId = incident.ProjectId;
        EnvironmentId = incident.EnvironmentId;
        Status = status;
        Visibility = visibility;
        Message = message.Trim();
        CreatedByUserId = createdByUserId;
        CreatedByEmail = string.IsNullOrWhiteSpace(createdByEmail) ? "system" : createdByEmail.Trim();
        CreatedAt = now;
    }
}
