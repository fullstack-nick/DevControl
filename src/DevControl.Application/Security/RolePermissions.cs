using DevControl.Domain.Enums;

namespace DevControl.Application.Security;

public static class RolePermissions
{
    public static bool AtLeast(OrganizationRole actual, OrganizationRole required) => actual >= required;

    public static bool CanRead(OrganizationRole role) => AtLeast(role, OrganizationRole.Viewer);

    public static bool CanManageProjects(OrganizationRole role) => AtLeast(role, OrganizationRole.Developer);

    public static bool CanManageOrganization(OrganizationRole role) => AtLeast(role, OrganizationRole.Admin);

    public static bool CanManageMembers(OrganizationRole role) => AtLeast(role, OrganizationRole.Admin);

    public static bool CanReadAudit(OrganizationRole role) => AtLeast(role, OrganizationRole.Admin);

    public static bool CanManageOwnerRole(OrganizationRole actorRole) => actorRole == OrganizationRole.Owner;
}
