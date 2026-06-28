using DevControl.Application.Security;
using DevControl.Domain.Entities;
using DevControl.Domain.Enums;
using DevControl.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;

namespace DevControl.Api.Security;

public sealed record TenantAccess(Organization Organization, OrganizationMember Member);

public enum TenantAccessStatus
{
    Granted,
    NotFound,
    Forbidden
}

public sealed record TenantAccessResult(TenantAccessStatus Status, TenantAccess? Access)
{
    public static TenantAccessResult Granted(TenantAccess access) => new(TenantAccessStatus.Granted, access);

    public static TenantAccessResult NotFound() => new(TenantAccessStatus.NotFound, null);

    public static TenantAccessResult Forbidden(TenantAccess access) => new(TenantAccessStatus.Forbidden, access);
}

public sealed class TenantAccessService(
    DevControlDbContext dbContext,
    AuditLogWriter auditLogWriter)
{
    public async Task<TenantAccessResult> RequireAsync(
        Guid organizationId,
        CurrentUser actor,
        OrganizationRole requiredRole,
        CancellationToken cancellationToken,
        bool auditDenied = false,
        string deniedAction = "authorization.denied",
        string targetType = "organization",
        string? targetId = null)
    {
        var organization = await dbContext.Organizations
            .SingleOrDefaultAsync(candidate => candidate.Id == organizationId, cancellationToken);

        if (organization is null)
        {
            return TenantAccessResult.NotFound();
        }

        var member = await dbContext.OrganizationMembers
            .SingleOrDefaultAsync(candidate =>
                    candidate.OrganizationId == organizationId &&
                    candidate.UserId == actor.Id &&
                    candidate.IsActive,
                cancellationToken);

        if (member is null)
        {
            return TenantAccessResult.NotFound();
        }

        var access = new TenantAccess(organization, member);
        if (RolePermissions.AtLeast(member.Role, requiredRole))
        {
            return TenantAccessResult.Granted(access);
        }

        if (auditDenied)
        {
            auditLogWriter.Add(
                organizationId,
                actor,
                deniedAction,
                "Denied",
                targetType,
                targetId ?? organizationId.ToString(),
                $"Denied {deniedAction} because {member.Role} is below required role {requiredRole}.",
                new { member.Role, requiredRole });

            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return TenantAccessResult.Forbidden(access);
    }

    public async Task<bool> HasAnotherActiveOwnerAsync(
        Guid organizationId,
        Guid memberId,
        CancellationToken cancellationToken)
    {
        return await dbContext.OrganizationMembers.AnyAsync(
            member =>
                member.OrganizationId == organizationId &&
                member.Id != memberId &&
                member.IsActive &&
                member.Role == OrganizationRole.Owner,
            cancellationToken);
    }
}
