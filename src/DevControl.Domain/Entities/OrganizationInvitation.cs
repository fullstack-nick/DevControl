using DevControl.Domain.Enums;

namespace DevControl.Domain.Entities;

public sealed class OrganizationInvitation
{
    public Guid Id { get; private set; }

    public Guid OrganizationId { get; private set; }

    public string Email { get; private set; } = string.Empty;

    public string NormalizedEmail { get; private set; } = string.Empty;

    public OrganizationRole Role { get; private set; }

    public string TokenHash { get; private set; } = string.Empty;

    public InvitationStatus Status { get; private set; }

    public Guid InvitedByUserId { get; private set; }

    public Guid? AcceptedByUserId { get; private set; }

    public DateTimeOffset ExpiresAt { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public DateTimeOffset LastSentAt { get; private set; }

    public DateTimeOffset? AcceptedAt { get; private set; }

    public DateTimeOffset? RevokedAt { get; private set; }

    private OrganizationInvitation()
    {
    }

    public OrganizationInvitation(
        Guid organizationId,
        string email,
        string normalizedEmail,
        OrganizationRole role,
        string tokenHash,
        Guid invitedByUserId,
        DateTimeOffset expiresAt,
        DateTimeOffset now)
    {
        Id = Guid.NewGuid();
        OrganizationId = organizationId;
        InvitedByUserId = invitedByUserId;
        CreatedAt = now;
        Status = InvitationStatus.Pending;
        UpdateDelivery(email, normalizedEmail, role, tokenHash, expiresAt, now);
    }

    public void UpdateDelivery(
        string email,
        string normalizedEmail,
        OrganizationRole role,
        string tokenHash,
        DateTimeOffset expiresAt,
        DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            throw new ArgumentException("Invitation email is required.", nameof(email));
        }

        if (string.IsNullOrWhiteSpace(normalizedEmail))
        {
            throw new ArgumentException("Normalized invitation email is required.", nameof(normalizedEmail));
        }

        if (string.IsNullOrWhiteSpace(tokenHash))
        {
            throw new ArgumentException("Invitation token hash is required.", nameof(tokenHash));
        }

        Email = email.Trim();
        NormalizedEmail = normalizedEmail.Trim();
        Role = role;
        TokenHash = tokenHash;
        ExpiresAt = expiresAt;
        LastSentAt = now;
        UpdatedAt = now;
    }

    public void MarkAccepted(Guid acceptedByUserId, DateTimeOffset now)
    {
        Status = InvitationStatus.Accepted;
        AcceptedByUserId = acceptedByUserId;
        AcceptedAt = now;
        UpdatedAt = now;
    }

    public void MarkRevoked(DateTimeOffset now)
    {
        Status = InvitationStatus.Revoked;
        RevokedAt = now;
        UpdatedAt = now;
    }

    public void MarkExpired(DateTimeOffset now)
    {
        Status = InvitationStatus.Expired;
        UpdatedAt = now;
    }

    public bool IsExpired(DateTimeOffset now) => ExpiresAt <= now;
}
