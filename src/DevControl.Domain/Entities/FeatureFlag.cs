using DevControl.Domain.Enums;

namespace DevControl.Domain.Entities;

public sealed class FeatureFlag
{
    public Guid Id { get; private set; }

    public Guid OrganizationId { get; private set; }

    public Guid ProjectId { get; private set; }

    public Guid EnvironmentId { get; private set; }

    public string Key { get; private set; } = string.Empty;

    public string Name { get; private set; } = string.Empty;

    public string Description { get; private set; } = string.Empty;

    public FeatureFlagKind Kind { get; private set; }

    public bool IsEnabled { get; private set; }

    public Guid CreatedByUserId { get; private set; }

    public Guid LastChangedByUserId { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public DateTimeOffset LastChangedAt { get; private set; }

    private FeatureFlag()
    {
    }

    public FeatureFlag(
        Guid organizationId,
        Guid projectId,
        Guid environmentId,
        string key,
        string name,
        string description,
        FeatureFlagKind kind,
        bool isEnabled,
        Guid createdByUserId,
        DateTimeOffset now)
    {
        Id = Guid.NewGuid();
        OrganizationId = organizationId;
        ProjectId = projectId;
        EnvironmentId = environmentId;
        Key = Require(key, nameof(key));
        Name = Require(name, nameof(name));
        Description = string.IsNullOrWhiteSpace(description) ? string.Empty : description.Trim();
        Kind = kind;
        IsEnabled = isEnabled;
        CreatedByUserId = createdByUserId;
        LastChangedByUserId = createdByUserId;
        CreatedAt = now;
        UpdatedAt = now;
        LastChangedAt = now;
    }

    public void Update(
        string name,
        string description,
        bool isEnabled,
        Guid changedByUserId,
        DateTimeOffset now)
    {
        Name = Require(name, nameof(name));
        Description = string.IsNullOrWhiteSpace(description) ? string.Empty : description.Trim();
        if (IsEnabled != isEnabled)
        {
            IsEnabled = isEnabled;
            LastChangedAt = now;
        }

        LastChangedByUserId = changedByUserId;
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
