namespace DevControl.Domain.Entities;

public sealed class SchemaVersion
{
    public int Id { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; private set; }

    private SchemaVersion()
    {
    }

    public SchemaVersion(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Schema version name is required.", nameof(name));
        }

        Name = name;
        CreatedAt = DateTimeOffset.UtcNow;
    }
}

