namespace DevControl.Domain.Entities;

public sealed class User
{
    public Guid Id { get; private set; }

    public string Email { get; private set; } = string.Empty;

    public string NormalizedEmail { get; private set; } = string.Empty;

    public string DisplayName { get; private set; } = string.Empty;

    public string ExternalProvider { get; private set; } = string.Empty;

    public string ExternalSubject { get; private set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public DateTimeOffset LastSeenAt { get; private set; }

    private User()
    {
    }

    public User(
        string email,
        string normalizedEmail,
        string displayName,
        string externalProvider,
        string externalSubject,
        DateTimeOffset now)
    {
        Id = Guid.NewGuid();
        CreatedAt = now;
        SetIdentity(email, normalizedEmail, displayName, externalProvider, externalSubject, now);
        LastSeenAt = now;
    }

    public void SetIdentity(
        string email,
        string normalizedEmail,
        string displayName,
        string externalProvider,
        string externalSubject,
        DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            throw new ArgumentException("User email is required.", nameof(email));
        }

        if (string.IsNullOrWhiteSpace(normalizedEmail))
        {
            throw new ArgumentException("Normalized user email is required.", nameof(normalizedEmail));
        }

        if (string.IsNullOrWhiteSpace(externalProvider))
        {
            throw new ArgumentException("External provider is required.", nameof(externalProvider));
        }

        Email = email.Trim();
        NormalizedEmail = normalizedEmail.Trim();
        DisplayName = string.IsNullOrWhiteSpace(displayName) ? Email : displayName.Trim();
        ExternalProvider = externalProvider.Trim();
        ExternalSubject = externalSubject.Trim();
        UpdatedAt = now;
    }

    public void MarkSeen(DateTimeOffset now)
    {
        LastSeenAt = now;
        UpdatedAt = now;
    }
}
