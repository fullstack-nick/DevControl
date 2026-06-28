namespace DevControl.Domain.Entities;

public sealed class Project
{
    public Guid Id { get; private set; }

    public Guid OrganizationId { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public string Slug { get; private set; } = string.Empty;

    public string Description { get; private set; } = string.Empty;

    public Guid CreatedByUserId { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    private Project()
    {
    }

    public Project(
        Guid organizationId,
        string name,
        string slug,
        string description,
        Guid createdByUserId,
        DateTimeOffset now)
    {
        Id = Guid.NewGuid();
        OrganizationId = organizationId;
        CreatedByUserId = createdByUserId;
        CreatedAt = now;
        Update(name, slug, description, now);
    }

    public void Update(string name, string slug, string description, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Project name is required.", nameof(name));
        }

        if (string.IsNullOrWhiteSpace(slug))
        {
            throw new ArgumentException("Project slug is required.", nameof(slug));
        }

        Name = name.Trim();
        Slug = slug.Trim();
        Description = description.Trim();
        UpdatedAt = now;
    }
}
