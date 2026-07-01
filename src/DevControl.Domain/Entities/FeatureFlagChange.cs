namespace DevControl.Domain.Entities;

public sealed class FeatureFlagChange
{
    public Guid Id { get; private set; }

    public Guid FeatureFlagId { get; private set; }

    public Guid OrganizationId { get; private set; }

    public Guid ProjectId { get; private set; }

    public Guid EnvironmentId { get; private set; }

    public bool OldValue { get; private set; }

    public bool NewValue { get; private set; }

    public string Reason { get; private set; } = string.Empty;

    public Guid ChangedByUserId { get; private set; }

    public DateTimeOffset ChangedAt { get; private set; }

    private FeatureFlagChange()
    {
    }

    public FeatureFlagChange(
        Guid featureFlagId,
        Guid organizationId,
        Guid projectId,
        Guid environmentId,
        bool oldValue,
        bool newValue,
        string reason,
        Guid changedByUserId,
        DateTimeOffset now)
    {
        Id = Guid.NewGuid();
        FeatureFlagId = featureFlagId;
        OrganizationId = organizationId;
        ProjectId = projectId;
        EnvironmentId = environmentId;
        OldValue = oldValue;
        NewValue = newValue;
        Reason = string.IsNullOrWhiteSpace(reason) ? string.Empty : reason.Trim();
        ChangedByUserId = changedByUserId;
        ChangedAt = now;
    }
}
