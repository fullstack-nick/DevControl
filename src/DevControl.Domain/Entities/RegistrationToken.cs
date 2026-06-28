namespace DevControl.Domain.Entities;

public sealed class RegistrationToken
{
    public Guid Id { get; private set; }

    public Guid OrganizationId { get; private set; }

    public Guid ProjectId { get; private set; }

    public Guid EnvironmentId { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public string TokenPrefix { get; private set; } = string.Empty;

    public string TokenHash { get; private set; } = string.Empty;

    public string Scope { get; private set; } = string.Empty;

    public Guid CreatedByUserId { get; private set; }

    public Guid? RevokedByUserId { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset? LastUsedAt { get; private set; }

    public DateTimeOffset? RevokedAt { get; private set; }

    public bool IsRevoked => RevokedAt is not null;

    private RegistrationToken()
    {
    }

    public RegistrationToken(
        Guid organizationId,
        Guid projectId,
        Guid environmentId,
        string name,
        string tokenPrefix,
        string tokenHash,
        string scope,
        Guid createdByUserId,
        DateTimeOffset now)
    {
        Id = Guid.NewGuid();
        OrganizationId = organizationId;
        ProjectId = projectId;
        EnvironmentId = environmentId;
        CreatedByUserId = createdByUserId;
        CreatedAt = now;
        Name = Require(name, nameof(name));
        TokenPrefix = Require(tokenPrefix, nameof(tokenPrefix));
        TokenHash = Require(tokenHash, nameof(tokenHash));
        Scope = Require(scope, nameof(scope));
    }

    public void MarkUsed(DateTimeOffset now)
    {
        LastUsedAt = now;
    }

    public void Revoke(Guid revokedByUserId, DateTimeOffset now)
    {
        if (RevokedAt is not null)
        {
            return;
        }

        RevokedByUserId = revokedByUserId;
        RevokedAt = now;
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
