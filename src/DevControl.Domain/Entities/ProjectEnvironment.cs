namespace DevControl.Domain.Entities;

public sealed class ProjectEnvironment
{
    public Guid Id { get; private set; }

    public Guid OrganizationId { get; private set; }

    public Guid ProjectId { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public string Slug { get; private set; } = string.Empty;

    public Guid CreatedByUserId { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    private ProjectEnvironment()
    {
    }

    public ProjectEnvironment(
        Guid organizationId,
        Guid projectId,
        string name,
        string slug,
        Guid createdByUserId,
        DateTimeOffset now)
    {
        Id = Guid.NewGuid();
        OrganizationId = organizationId;
        ProjectId = projectId;
        CreatedByUserId = createdByUserId;
        CreatedAt = now;
        Update(name, slug, now);
    }

    public void Update(string name, string slug, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Environment name is required.", nameof(name));
        }

        if (string.IsNullOrWhiteSpace(slug))
        {
            throw new ArgumentException("Environment slug is required.", nameof(slug));
        }

        Name = name.Trim();
        Slug = slug.Trim();
        UpdatedAt = now;
    }
}
