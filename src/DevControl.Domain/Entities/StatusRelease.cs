using DevControl.Domain.Enums;

namespace DevControl.Domain.Entities;

public sealed class StatusRelease
{
    public Guid Id { get; private set; }

    public Guid OrganizationId { get; private set; }

    public Guid ProjectId { get; private set; }

    public Guid EnvironmentId { get; private set; }

    public string Title { get; private set; } = string.Empty;

    public string Version { get; private set; } = string.Empty;

    public string Body { get; private set; } = string.Empty;

    public ReleaseStatus Status { get; private set; }

    public Guid CreatedByUserId { get; private set; }

    public Guid? PublishedByUserId { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public DateTimeOffset? PublishedAt { get; private set; }

    private StatusRelease()
    {
    }

    public StatusRelease(
        Guid organizationId,
        Guid projectId,
        Guid environmentId,
        string title,
        string version,
        string body,
        Guid createdByUserId,
        DateTimeOffset now)
    {
        Id = Guid.NewGuid();
        OrganizationId = organizationId;
        ProjectId = projectId;
        EnvironmentId = environmentId;
        CreatedByUserId = createdByUserId;
        Status = ReleaseStatus.Draft;
        CreatedAt = now;
        Update(title, version, body, createdByUserId, now);
    }

    public void Update(string title, string version, string body, Guid updatedByUserId, DateTimeOffset now)
    {
        if (Status == ReleaseStatus.Published)
        {
            throw new InvalidOperationException("Published releases cannot be edited.");
        }

        Title = Require(title, nameof(title), 200);
        Version = Require(version, nameof(version), 120);
        Body = Require(body, nameof(body), 8000);
        UpdatedAt = now;
    }

    public void Publish(Guid publishedByUserId, DateTimeOffset now)
    {
        Status = ReleaseStatus.Published;
        PublishedByUserId = publishedByUserId;
        PublishedAt = now;
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
