namespace DevControl.Domain.Entities;

public sealed class Organization
{
    public Guid Id { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public string Slug { get; private set; } = string.Empty;

    public Guid CreatedByUserId { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    private Organization()
    {
    }

    public Organization(string name, string slug, Guid createdByUserId, DateTimeOffset now)
    {
        Id = Guid.NewGuid();
        CreatedByUserId = createdByUserId;
        CreatedAt = now;
        Update(name, slug, now);
    }

    public void Update(string name, string slug, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Organization name is required.", nameof(name));
        }

        if (string.IsNullOrWhiteSpace(slug))
        {
            throw new ArgumentException("Organization slug is required.", nameof(slug));
        }

        Name = name.Trim();
        Slug = slug.Trim();
        UpdatedAt = now;
    }
}
