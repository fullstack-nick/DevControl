using DevControl.Domain.Enums;

namespace DevControl.Domain.Entities;

public sealed class OrganizationMember
{
    public Guid Id { get; private set; }

    public Guid OrganizationId { get; private set; }

    public Guid UserId { get; private set; }

    public OrganizationRole Role { get; private set; }

    public bool IsActive { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public DateTimeOffset? RemovedAt { get; private set; }

    private OrganizationMember()
    {
    }

    public OrganizationMember(Guid organizationId, Guid userId, OrganizationRole role, DateTimeOffset now)
    {
        Id = Guid.NewGuid();
        OrganizationId = organizationId;
        UserId = userId;
        Role = role;
        IsActive = true;
        CreatedAt = now;
        UpdatedAt = now;
    }

    public void ChangeRole(OrganizationRole role, DateTimeOffset now)
    {
        Role = role;
        UpdatedAt = now;
    }

    public void Deactivate(DateTimeOffset now)
    {
        IsActive = false;
        RemovedAt = now;
        UpdatedAt = now;
    }

    public void Reactivate(OrganizationRole role, DateTimeOffset now)
    {
        Role = role;
        IsActive = true;
        RemovedAt = null;
        UpdatedAt = now;
    }
}
