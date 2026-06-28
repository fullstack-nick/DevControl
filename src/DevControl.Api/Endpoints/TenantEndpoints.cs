using System.Net;
using DevControl.Api.Security;
using DevControl.Application.Email;
using DevControl.Application.Security;
using DevControl.Domain.Entities;
using DevControl.Domain.Enums;
using DevControl.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;

namespace DevControl.Api.Endpoints;

public static class TenantEndpoints
{
    public static void MapTenantEndpoints(this WebApplication app)
    {
        var api = app.MapGroup("/api").RequireAuthorization();

        api.MapGet("/organizations", ListOrganizationsAsync);
        api.MapPost("/organizations", CreateOrganizationAsync).RequireCsrf();
        api.MapGet("/organizations/{organizationId:guid}", GetOrganizationAsync);
        api.MapPatch("/organizations/{organizationId:guid}", UpdateOrganizationAsync).RequireCsrf();

        api.MapGet("/organizations/{organizationId:guid}/members", ListMembersAsync);
        api.MapPatch("/organizations/{organizationId:guid}/members/{memberId:guid}", UpdateMemberAsync).RequireCsrf();
        api.MapDelete("/organizations/{organizationId:guid}/members/{memberId:guid}", RemoveMemberAsync).RequireCsrf();

        api.MapGet("/organizations/{organizationId:guid}/invitations", ListInvitationsAsync);
        api.MapPost("/organizations/{organizationId:guid}/invitations", CreateInvitationAsync).RequireCsrf();
        api.MapPost("/organizations/{organizationId:guid}/invitations/{invitationId:guid}/revoke", RevokeInvitationAsync).RequireCsrf();
        api.MapPost("/organizations/{organizationId:guid}/invitations/{invitationId:guid}/resend", ResendInvitationAsync).RequireCsrf();
        api.MapPost("/invitations/{token}/accept", AcceptInvitationAsync).RequireCsrf();

        api.MapGet("/organizations/{organizationId:guid}/projects", ListProjectsAsync);
        api.MapPost("/organizations/{organizationId:guid}/projects", CreateProjectAsync).RequireCsrf();
        api.MapGet("/organizations/{organizationId:guid}/projects/{projectId:guid}", GetProjectAsync);
        api.MapPatch("/organizations/{organizationId:guid}/projects/{projectId:guid}", UpdateProjectAsync).RequireCsrf();

        api.MapGet("/organizations/{organizationId:guid}/projects/{projectId:guid}/environments", ListEnvironmentsAsync);
        api.MapPost("/organizations/{organizationId:guid}/projects/{projectId:guid}/environments", CreateEnvironmentAsync).RequireCsrf();
        api.MapGet("/organizations/{organizationId:guid}/projects/{projectId:guid}/environments/{environmentId:guid}", GetEnvironmentAsync);
        api.MapPatch("/organizations/{organizationId:guid}/projects/{projectId:guid}/environments/{environmentId:guid}", UpdateEnvironmentAsync).RequireCsrf();

        api.MapGet("/organizations/{organizationId:guid}/audit-logs", ListAuditLogsAsync);
        api.MapGet("/organizations/{organizationId:guid}/control-actions", ListControlActionsAsync);
    }

    private static async Task<IResult> ListOrganizationsAsync(
        CurrentUserAccessor currentUserAccessor,
        DevControlDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var actor = await currentUserAccessor.GetOrCreateAsync(cancellationToken);
        var organizations = await dbContext.OrganizationMembers
            .Where(member => member.UserId == actor.Id && member.IsActive)
            .Join(
                dbContext.Organizations,
                member => member.OrganizationId,
                organization => organization.Id,
                (member, organization) => new { member, organization })
            .OrderBy(candidate => candidate.organization.Name)
            .Select(candidate => new OrganizationResponse(
                candidate.organization.Id,
                candidate.organization.Name,
                candidate.organization.Slug,
                candidate.member.Role.ToString(),
                candidate.organization.CreatedAt,
                candidate.organization.UpdatedAt))
            .ToListAsync(cancellationToken);

        return Results.Ok(organizations);
    }

    private static async Task<IResult> CreateOrganizationAsync(
        OrganizationUpsertRequest request,
        CurrentUserAccessor currentUserAccessor,
        DevControlDbContext dbContext,
        AuditLogWriter auditLogWriter,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var actor = await currentUserAccessor.GetOrCreateAsync(cancellationToken);
        var validation = NormalizeNameAndSlug(request.Name, request.Slug, out var name, out var slug);
        if (validation is not null)
        {
            return validation;
        }

        if (await dbContext.Organizations.AnyAsync(organization => organization.Slug == slug, cancellationToken))
        {
            return Conflict("Organization slug is already in use.");
        }

        var now = timeProvider.GetUtcNow();
        var organization = new Organization(name, slug, actor.Id, now);
        var ownerMembership = new OrganizationMember(organization.Id, actor.Id, OrganizationRole.Owner, now);

        dbContext.Organizations.Add(organization);
        dbContext.OrganizationMembers.Add(ownerMembership);
        auditLogWriter.Add(
            organization.Id,
            actor,
            "organization.create",
            "Succeeded",
            "organization",
            organization.Id.ToString(),
            "Organization created.",
            new { organization.Name, organization.Slug });

        await dbContext.SaveChangesAsync(cancellationToken);
        return Results.Created(
            $"/api/organizations/{organization.Id}",
            new OrganizationResponse(organization.Id, organization.Name, organization.Slug, OrganizationRole.Owner.ToString(), organization.CreatedAt, organization.UpdatedAt));
    }

    private static async Task<IResult> GetOrganizationAsync(
        Guid organizationId,
        CurrentUserAccessor currentUserAccessor,
        TenantAccessService tenantAccess,
        CancellationToken cancellationToken)
    {
        var actor = await currentUserAccessor.GetOrCreateAsync(cancellationToken);
        var access = await tenantAccess.RequireAsync(organizationId, actor, OrganizationRole.Viewer, cancellationToken);
        var failure = AccessFailure(access);
        if (failure is not null)
        {
            return failure;
        }

        var organization = access.Access!.Organization;
        return Results.Ok(new OrganizationResponse(
            organization.Id,
            organization.Name,
            organization.Slug,
            access.Access.Member.Role.ToString(),
            organization.CreatedAt,
            organization.UpdatedAt));
    }

    private static async Task<IResult> UpdateOrganizationAsync(
        Guid organizationId,
        OrganizationUpsertRequest request,
        CurrentUserAccessor currentUserAccessor,
        TenantAccessService tenantAccess,
        DevControlDbContext dbContext,
        AuditLogWriter auditLogWriter,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var actor = await currentUserAccessor.GetOrCreateAsync(cancellationToken);
        var access = await tenantAccess.RequireAsync(
            organizationId,
            actor,
            OrganizationRole.Admin,
            cancellationToken,
            auditDenied: true,
            deniedAction: "organization.update.denied",
            targetId: organizationId.ToString());
        var failure = AccessFailure(access);
        if (failure is not null)
        {
            return failure;
        }

        var validation = NormalizeNameAndSlug(request.Name, request.Slug, out var name, out var slug);
        if (validation is not null)
        {
            return validation;
        }

        if (await dbContext.Organizations.AnyAsync(
                organization => organization.Id != organizationId && organization.Slug == slug,
                cancellationToken))
        {
            return Conflict("Organization slug is already in use.");
        }

        var organization = access.Access!.Organization;
        organization.Update(name, slug, timeProvider.GetUtcNow());
        auditLogWriter.Add(
            organization.Id,
            actor,
            "organization.update",
            "Succeeded",
            "organization",
            organization.Id.ToString(),
            "Organization updated.",
            new { organization.Name, organization.Slug });

        await dbContext.SaveChangesAsync(cancellationToken);
        return Results.Ok(new OrganizationResponse(
            organization.Id,
            organization.Name,
            organization.Slug,
            access.Access.Member.Role.ToString(),
            organization.CreatedAt,
            organization.UpdatedAt));
    }

    private static async Task<IResult> ListMembersAsync(
        Guid organizationId,
        CurrentUserAccessor currentUserAccessor,
        TenantAccessService tenantAccess,
        DevControlDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var actor = await currentUserAccessor.GetOrCreateAsync(cancellationToken);
        var access = await tenantAccess.RequireAsync(organizationId, actor, OrganizationRole.Admin, cancellationToken);
        var failure = AccessFailure(access);
        if (failure is not null)
        {
            return failure;
        }

        var members = await dbContext.OrganizationMembers
            .Where(member => member.OrganizationId == organizationId && member.IsActive)
            .Join(
                dbContext.Users,
                member => member.UserId,
                user => user.Id,
                (member, user) => new { member, user })
            .OrderBy(candidate => candidate.user.Email)
            .Select(candidate => new MemberResponse(
                candidate.member.Id,
                candidate.user.Id,
                candidate.user.Email,
                candidate.user.DisplayName,
                candidate.member.Role.ToString(),
                candidate.member.CreatedAt,
                candidate.member.UpdatedAt))
            .ToListAsync(cancellationToken);

        return Results.Ok(members);
    }

    private static async Task<IResult> UpdateMemberAsync(
        Guid organizationId,
        Guid memberId,
        MemberUpdateRequest request,
        CurrentUserAccessor currentUserAccessor,
        TenantAccessService tenantAccess,
        DevControlDbContext dbContext,
        AuditLogWriter auditLogWriter,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var actor = await currentUserAccessor.GetOrCreateAsync(cancellationToken);
        var access = await tenantAccess.RequireAsync(
            organizationId,
            actor,
            OrganizationRole.Admin,
            cancellationToken,
            auditDenied: true,
            deniedAction: "member.update.denied",
            targetType: "member",
            targetId: memberId.ToString());
        var failure = AccessFailure(access);
        if (failure is not null)
        {
            return failure;
        }

        if (!TryParseRole(request.Role, out var newRole, out var roleFailure))
        {
            return roleFailure;
        }

        var member = await dbContext.OrganizationMembers
            .SingleOrDefaultAsync(candidate => candidate.OrganizationId == organizationId && candidate.Id == memberId && candidate.IsActive, cancellationToken);
        if (member is null)
        {
            return Results.NotFound();
        }

        if ((member.Role == OrganizationRole.Owner || newRole == OrganizationRole.Owner) &&
            !RolePermissions.CanManageOwnerRole(access.Access!.Member.Role))
        {
            AddDeniedAudit(auditLogWriter, organizationId, actor, "member.update.denied", "member", memberId.ToString(), "Only owners can assign or change owner roles.");
            await dbContext.SaveChangesAsync(cancellationToken);
            return Results.Forbid();
        }

        if (member.Role == OrganizationRole.Owner &&
            newRole != OrganizationRole.Owner &&
            !await tenantAccess.HasAnotherActiveOwnerAsync(organizationId, member.Id, cancellationToken))
        {
            return Results.BadRequest(new ProblemDetailsResponse("Organizations must keep at least one active owner."));
        }

        member.ChangeRole(newRole, timeProvider.GetUtcNow());
        auditLogWriter.Add(
            organizationId,
            actor,
            "member.update",
            "Succeeded",
            "member",
            member.Id.ToString(),
            "Organization member role updated.",
            new { member.UserId, role = newRole.ToString() });

        await dbContext.SaveChangesAsync(cancellationToken);
        return Results.Ok(new MemberRoleResponse(member.Id, newRole.ToString()));
    }

    private static async Task<IResult> RemoveMemberAsync(
        Guid organizationId,
        Guid memberId,
        CurrentUserAccessor currentUserAccessor,
        TenantAccessService tenantAccess,
        DevControlDbContext dbContext,
        AuditLogWriter auditLogWriter,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var actor = await currentUserAccessor.GetOrCreateAsync(cancellationToken);
        var access = await tenantAccess.RequireAsync(
            organizationId,
            actor,
            OrganizationRole.Admin,
            cancellationToken,
            auditDenied: true,
            deniedAction: "member.remove.denied",
            targetType: "member",
            targetId: memberId.ToString());
        var failure = AccessFailure(access);
        if (failure is not null)
        {
            return failure;
        }

        var member = await dbContext.OrganizationMembers
            .SingleOrDefaultAsync(candidate => candidate.OrganizationId == organizationId && candidate.Id == memberId && candidate.IsActive, cancellationToken);
        if (member is null)
        {
            return Results.NotFound();
        }

        if (member.Role == OrganizationRole.Owner && !RolePermissions.CanManageOwnerRole(access.Access!.Member.Role))
        {
            AddDeniedAudit(auditLogWriter, organizationId, actor, "member.remove.denied", "member", memberId.ToString(), "Only owners can remove owners.");
            await dbContext.SaveChangesAsync(cancellationToken);
            return Results.Forbid();
        }

        if (member.Role == OrganizationRole.Owner &&
            !await tenantAccess.HasAnotherActiveOwnerAsync(organizationId, member.Id, cancellationToken))
        {
            return Results.BadRequest(new ProblemDetailsResponse("Organizations must keep at least one active owner."));
        }

        member.Deactivate(timeProvider.GetUtcNow());
        auditLogWriter.Add(
            organizationId,
            actor,
            "member.remove",
            "Succeeded",
            "member",
            member.Id.ToString(),
            "Organization member removed.",
            new { member.UserId, member.Role });

        await dbContext.SaveChangesAsync(cancellationToken);
        return Results.NoContent();
    }

    private static async Task<IResult> ListInvitationsAsync(
        Guid organizationId,
        CurrentUserAccessor currentUserAccessor,
        TenantAccessService tenantAccess,
        DevControlDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var actor = await currentUserAccessor.GetOrCreateAsync(cancellationToken);
        var access = await tenantAccess.RequireAsync(organizationId, actor, OrganizationRole.Admin, cancellationToken);
        var failure = AccessFailure(access);
        if (failure is not null)
        {
            return failure;
        }

        var invitations = await dbContext.OrganizationInvitations
            .Where(invitation => invitation.OrganizationId == organizationId)
            .OrderByDescending(invitation => invitation.CreatedAt)
            .Take(100)
            .Select(invitation => new InvitationResponse(
                invitation.Id,
                invitation.Email,
                invitation.Role.ToString(),
                invitation.Status.ToString(),
                invitation.ExpiresAt,
                invitation.LastSentAt,
                invitation.AcceptedAt,
                invitation.RevokedAt))
            .ToListAsync(cancellationToken);

        return Results.Ok(invitations);
    }

    private static async Task<IResult> CreateInvitationAsync(
        Guid organizationId,
        InvitationCreateRequest request,
        HttpContext httpContext,
        CurrentUserAccessor currentUserAccessor,
        TenantAccessService tenantAccess,
        DevControlDbContext dbContext,
        AuditLogWriter auditLogWriter,
        InvitationTokenService invitationTokenService,
        IEmailSender emailSender,
        TimeProvider timeProvider,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        var actor = await currentUserAccessor.GetOrCreateAsync(cancellationToken);
        var access = await tenantAccess.RequireAsync(
            organizationId,
            actor,
            OrganizationRole.Admin,
            cancellationToken,
            auditDenied: true,
            deniedAction: "invitation.create.denied",
            targetType: "invitation");
        var failure = AccessFailure(access);
        if (failure is not null)
        {
            return failure;
        }

        if (!TryParseRole(request.Role, out var role, out var roleFailure))
        {
            return roleFailure;
        }

        if (role == OrganizationRole.Owner && !RolePermissions.CanManageOwnerRole(access.Access!.Member.Role))
        {
            AddDeniedAudit(auditLogWriter, organizationId, actor, "invitation.create.denied", "invitation", null, "Only owners can invite owners.");
            await dbContext.SaveChangesAsync(cancellationToken);
            return Results.Forbid();
        }

        var normalizedEmail = NormalizeInvitationEmail(request.Email, out var emailFailure);
        if (emailFailure is not null)
        {
            return emailFailure;
        }

        var existingUser = await dbContext.Users.SingleOrDefaultAsync(user => user.NormalizedEmail == normalizedEmail, cancellationToken);
        if (existingUser is not null &&
            await dbContext.OrganizationMembers.AnyAsync(
                member => member.OrganizationId == organizationId && member.UserId == existingUser.Id && member.IsActive,
                cancellationToken))
        {
            return Conflict("That user is already an active organization member.");
        }

        var now = timeProvider.GetUtcNow();
        var token = invitationTokenService.CreateToken();
        var tokenHash = invitationTokenService.HashToken(token);
        var expiresAt = now.AddDays(7);
        var email = EmailAddressNormalizer.Display(request.Email!);
        var pendingInvitation = await dbContext.OrganizationInvitations
            .SingleOrDefaultAsync(invitation =>
                    invitation.OrganizationId == organizationId &&
                    invitation.NormalizedEmail == normalizedEmail &&
                    invitation.Status == InvitationStatus.Pending,
                cancellationToken);

        if (pendingInvitation is null)
        {
            pendingInvitation = new OrganizationInvitation(
                organizationId,
                email,
                normalizedEmail,
                role,
                tokenHash,
                actor.Id,
                expiresAt,
                now);
            dbContext.OrganizationInvitations.Add(pendingInvitation);
        }
        else
        {
            pendingInvitation.UpdateDelivery(email, normalizedEmail, role, tokenHash, expiresAt, now);
        }

        auditLogWriter.Add(
            organizationId,
            actor,
            "invitation.create",
            "Succeeded",
            "invitation",
            pendingInvitation.Id.ToString(),
            "Organization invitation created.",
            new { pendingInvitation.Email, role = role.ToString(), pendingInvitation.ExpiresAt });

        await dbContext.SaveChangesAsync(cancellationToken);
        await SendInvitationEmailAsync(httpContext, emailSender, loggerFactory, access.Access!.Organization.Name, pendingInvitation, token, cancellationToken);

        return Results.Created(
            $"/api/organizations/{organizationId}/invitations/{pendingInvitation.Id}",
            ToInvitationResponse(pendingInvitation));
    }

    private static async Task<IResult> RevokeInvitationAsync(
        Guid organizationId,
        Guid invitationId,
        CurrentUserAccessor currentUserAccessor,
        TenantAccessService tenantAccess,
        DevControlDbContext dbContext,
        AuditLogWriter auditLogWriter,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var actor = await currentUserAccessor.GetOrCreateAsync(cancellationToken);
        var access = await tenantAccess.RequireAsync(
            organizationId,
            actor,
            OrganizationRole.Admin,
            cancellationToken,
            auditDenied: true,
            deniedAction: "invitation.revoke.denied",
            targetType: "invitation",
            targetId: invitationId.ToString());
        var failure = AccessFailure(access);
        if (failure is not null)
        {
            return failure;
        }

        var invitation = await dbContext.OrganizationInvitations
            .SingleOrDefaultAsync(candidate => candidate.OrganizationId == organizationId && candidate.Id == invitationId, cancellationToken);
        if (invitation is null)
        {
            return Results.NotFound();
        }

        if (invitation.Status == InvitationStatus.Pending)
        {
            invitation.MarkRevoked(timeProvider.GetUtcNow());
            auditLogWriter.Add(
                organizationId,
                actor,
                "invitation.revoke",
                "Succeeded",
                "invitation",
                invitation.Id.ToString(),
                "Organization invitation revoked.",
                new { invitation.Email });
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return Results.Ok(ToInvitationResponse(invitation));
    }

    private static async Task<IResult> ResendInvitationAsync(
        Guid organizationId,
        Guid invitationId,
        HttpContext httpContext,
        CurrentUserAccessor currentUserAccessor,
        TenantAccessService tenantAccess,
        DevControlDbContext dbContext,
        AuditLogWriter auditLogWriter,
        InvitationTokenService invitationTokenService,
        IEmailSender emailSender,
        TimeProvider timeProvider,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        var actor = await currentUserAccessor.GetOrCreateAsync(cancellationToken);
        var access = await tenantAccess.RequireAsync(
            organizationId,
            actor,
            OrganizationRole.Admin,
            cancellationToken,
            auditDenied: true,
            deniedAction: "invitation.resend.denied",
            targetType: "invitation",
            targetId: invitationId.ToString());
        var failure = AccessFailure(access);
        if (failure is not null)
        {
            return failure;
        }

        var invitation = await dbContext.OrganizationInvitations
            .SingleOrDefaultAsync(candidate => candidate.OrganizationId == organizationId && candidate.Id == invitationId, cancellationToken);
        if (invitation is null)
        {
            return Results.NotFound();
        }

        if (invitation.Status != InvitationStatus.Pending)
        {
            return Results.BadRequest(new ProblemDetailsResponse("Only pending invitations can be resent."));
        }

        var now = timeProvider.GetUtcNow();
        var token = invitationTokenService.CreateToken();
        invitation.UpdateDelivery(
            invitation.Email,
            invitation.NormalizedEmail,
            invitation.Role,
            invitationTokenService.HashToken(token),
            now.AddDays(7),
            now);

        auditLogWriter.Add(
            organizationId,
            actor,
            "invitation.resend",
            "Succeeded",
            "invitation",
            invitation.Id.ToString(),
            "Organization invitation resent.",
            new { invitation.Email, invitation.ExpiresAt });

        await dbContext.SaveChangesAsync(cancellationToken);
        await SendInvitationEmailAsync(httpContext, emailSender, loggerFactory, access.Access!.Organization.Name, invitation, token, cancellationToken);

        return Results.Ok(ToInvitationResponse(invitation));
    }

    private static async Task<IResult> AcceptInvitationAsync(
        string token,
        CurrentUserAccessor currentUserAccessor,
        DevControlDbContext dbContext,
        AuditLogWriter auditLogWriter,
        InvitationTokenService invitationTokenService,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var actor = await currentUserAccessor.GetOrCreateAsync(cancellationToken);
        var tokenHash = invitationTokenService.HashToken(token);
        var invitation = await dbContext.OrganizationInvitations
            .SingleOrDefaultAsync(candidate => candidate.TokenHash == tokenHash, cancellationToken);
        if (invitation is null)
        {
            return Results.NotFound();
        }

        var now = timeProvider.GetUtcNow();
        if (invitation.Status != InvitationStatus.Pending)
        {
            return Results.BadRequest(new ProblemDetailsResponse("Invitation is no longer pending."));
        }

        if (invitation.IsExpired(now))
        {
            invitation.MarkExpired(now);
            auditLogWriter.Add(
                invitation.OrganizationId,
                actor,
                "invitation.accept.denied",
                "Denied",
                "invitation",
                invitation.Id.ToString(),
                "Invitation acceptance denied because the invitation expired.",
                new { invitation.Email, invitation.ExpiresAt });
            await dbContext.SaveChangesAsync(cancellationToken);
            return Results.Problem("Invitation has expired.", statusCode: StatusCodes.Status410Gone);
        }

        if (!string.Equals(invitation.NormalizedEmail, actor.NormalizedEmail, StringComparison.Ordinal))
        {
            auditLogWriter.Add(
                invitation.OrganizationId,
                actor,
                "invitation.accept.denied",
                "Denied",
                "invitation",
                invitation.Id.ToString(),
                "Invitation acceptance denied because the authenticated email does not match the invitation.",
                new { invitationEmail = invitation.Email, actorEmail = actor.Email });
            await dbContext.SaveChangesAsync(cancellationToken);
            return Results.Forbid();
        }

        var existingMember = await dbContext.OrganizationMembers
            .SingleOrDefaultAsync(member => member.OrganizationId == invitation.OrganizationId && member.UserId == actor.Id, cancellationToken);
        if (existingMember is null)
        {
            dbContext.OrganizationMembers.Add(new OrganizationMember(invitation.OrganizationId, actor.Id, invitation.Role, now));
        }
        else if (!existingMember.IsActive)
        {
            existingMember.Reactivate(invitation.Role, now);
        }

        invitation.MarkAccepted(actor.Id, now);
        auditLogWriter.Add(
            invitation.OrganizationId,
            actor,
            "invitation.accept",
            "Succeeded",
            "invitation",
            invitation.Id.ToString(),
            "Organization invitation accepted.",
            new { invitation.Email, role = invitation.Role.ToString() });

        await dbContext.SaveChangesAsync(cancellationToken);
        return Results.Ok(ToInvitationResponse(invitation));
    }

    private static async Task<IResult> ListProjectsAsync(
        Guid organizationId,
        CurrentUserAccessor currentUserAccessor,
        TenantAccessService tenantAccess,
        DevControlDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var actor = await currentUserAccessor.GetOrCreateAsync(cancellationToken);
        var access = await tenantAccess.RequireAsync(organizationId, actor, OrganizationRole.Viewer, cancellationToken);
        var failure = AccessFailure(access);
        if (failure is not null)
        {
            return failure;
        }

        var projects = await dbContext.Projects
            .Where(project => project.OrganizationId == organizationId)
            .OrderBy(project => project.Name)
            .Select(project => ToProjectResponse(project))
            .ToListAsync(cancellationToken);

        return Results.Ok(projects);
    }

    private static async Task<IResult> CreateProjectAsync(
        Guid organizationId,
        ProjectUpsertRequest request,
        CurrentUserAccessor currentUserAccessor,
        TenantAccessService tenantAccess,
        DevControlDbContext dbContext,
        AuditLogWriter auditLogWriter,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var actor = await currentUserAccessor.GetOrCreateAsync(cancellationToken);
        var access = await tenantAccess.RequireAsync(
            organizationId,
            actor,
            OrganizationRole.Developer,
            cancellationToken,
            auditDenied: true,
            deniedAction: "project.create.denied",
            targetType: "project");
        var failure = AccessFailure(access);
        if (failure is not null)
        {
            return failure;
        }

        var validation = NormalizeNameAndSlug(request.Name, request.Slug, out var name, out var slug);
        if (validation is not null)
        {
            return validation;
        }

        if (await dbContext.Projects.AnyAsync(project => project.OrganizationId == organizationId && project.Slug == slug, cancellationToken))
        {
            return Conflict("Project slug is already in use for this organization.");
        }

        var project = new Project(organizationId, name, slug, request.Description ?? string.Empty, actor.Id, timeProvider.GetUtcNow());
        dbContext.Projects.Add(project);
        auditLogWriter.Add(
            organizationId,
            actor,
            "project.create",
            "Succeeded",
            "project",
            project.Id.ToString(),
            "Project created.",
            new { project.Name, project.Slug });

        await dbContext.SaveChangesAsync(cancellationToken);
        return Results.Created($"/api/organizations/{organizationId}/projects/{project.Id}", ToProjectResponse(project));
    }

    private static async Task<IResult> GetProjectAsync(
        Guid organizationId,
        Guid projectId,
        CurrentUserAccessor currentUserAccessor,
        TenantAccessService tenantAccess,
        DevControlDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var actor = await currentUserAccessor.GetOrCreateAsync(cancellationToken);
        var access = await tenantAccess.RequireAsync(organizationId, actor, OrganizationRole.Viewer, cancellationToken);
        var failure = AccessFailure(access);
        if (failure is not null)
        {
            return failure;
        }

        var project = await dbContext.Projects
            .SingleOrDefaultAsync(candidate => candidate.OrganizationId == organizationId && candidate.Id == projectId, cancellationToken);
        return project is null ? Results.NotFound() : Results.Ok(ToProjectResponse(project));
    }

    private static async Task<IResult> UpdateProjectAsync(
        Guid organizationId,
        Guid projectId,
        ProjectUpsertRequest request,
        CurrentUserAccessor currentUserAccessor,
        TenantAccessService tenantAccess,
        DevControlDbContext dbContext,
        AuditLogWriter auditLogWriter,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var actor = await currentUserAccessor.GetOrCreateAsync(cancellationToken);
        var access = await tenantAccess.RequireAsync(
            organizationId,
            actor,
            OrganizationRole.Developer,
            cancellationToken,
            auditDenied: true,
            deniedAction: "project.update.denied",
            targetType: "project",
            targetId: projectId.ToString());
        var failure = AccessFailure(access);
        if (failure is not null)
        {
            return failure;
        }

        var project = await dbContext.Projects
            .SingleOrDefaultAsync(candidate => candidate.OrganizationId == organizationId && candidate.Id == projectId, cancellationToken);
        if (project is null)
        {
            return Results.NotFound();
        }

        var validation = NormalizeNameAndSlug(request.Name, request.Slug, out var name, out var slug);
        if (validation is not null)
        {
            return validation;
        }

        if (await dbContext.Projects.AnyAsync(
                candidate => candidate.OrganizationId == organizationId && candidate.Id != projectId && candidate.Slug == slug,
                cancellationToken))
        {
            return Conflict("Project slug is already in use for this organization.");
        }

        project.Update(name, slug, request.Description ?? string.Empty, timeProvider.GetUtcNow());
        auditLogWriter.Add(
            organizationId,
            actor,
            "project.update",
            "Succeeded",
            "project",
            project.Id.ToString(),
            "Project updated.",
            new { project.Name, project.Slug });

        await dbContext.SaveChangesAsync(cancellationToken);
        return Results.Ok(ToProjectResponse(project));
    }

    private static async Task<IResult> ListEnvironmentsAsync(
        Guid organizationId,
        Guid projectId,
        CurrentUserAccessor currentUserAccessor,
        TenantAccessService tenantAccess,
        DevControlDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var actor = await currentUserAccessor.GetOrCreateAsync(cancellationToken);
        var access = await tenantAccess.RequireAsync(organizationId, actor, OrganizationRole.Viewer, cancellationToken);
        var failure = AccessFailure(access);
        if (failure is not null)
        {
            return failure;
        }

        if (!await ProjectExistsAsync(dbContext, organizationId, projectId, cancellationToken))
        {
            return Results.NotFound();
        }

        var environments = await dbContext.ProjectEnvironments
            .Where(environment => environment.OrganizationId == organizationId && environment.ProjectId == projectId)
            .OrderBy(environment => environment.Name)
            .Select(environment => ToEnvironmentResponse(environment))
            .ToListAsync(cancellationToken);

        return Results.Ok(environments);
    }

    private static async Task<IResult> CreateEnvironmentAsync(
        Guid organizationId,
        Guid projectId,
        EnvironmentUpsertRequest request,
        CurrentUserAccessor currentUserAccessor,
        TenantAccessService tenantAccess,
        DevControlDbContext dbContext,
        AuditLogWriter auditLogWriter,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var actor = await currentUserAccessor.GetOrCreateAsync(cancellationToken);
        var access = await tenantAccess.RequireAsync(
            organizationId,
            actor,
            OrganizationRole.Developer,
            cancellationToken,
            auditDenied: true,
            deniedAction: "environment.create.denied",
            targetType: "environment");
        var failure = AccessFailure(access);
        if (failure is not null)
        {
            return failure;
        }

        if (!await ProjectExistsAsync(dbContext, organizationId, projectId, cancellationToken))
        {
            return Results.NotFound();
        }

        var validation = NormalizeNameAndSlug(request.Name, request.Slug, out var name, out var slug);
        if (validation is not null)
        {
            return validation;
        }

        if (await dbContext.ProjectEnvironments.AnyAsync(environment => environment.ProjectId == projectId && environment.Slug == slug, cancellationToken))
        {
            return Conflict("Environment slug is already in use for this project.");
        }

        var environment = new ProjectEnvironment(organizationId, projectId, name, slug, actor.Id, timeProvider.GetUtcNow());
        dbContext.ProjectEnvironments.Add(environment);
        auditLogWriter.Add(
            organizationId,
            actor,
            "environment.create",
            "Succeeded",
            "environment",
            environment.Id.ToString(),
            "Environment created.",
            new { environment.Name, environment.Slug },
            projectId);

        await dbContext.SaveChangesAsync(cancellationToken);
        return Results.Created(
            $"/api/organizations/{organizationId}/projects/{projectId}/environments/{environment.Id}",
            ToEnvironmentResponse(environment));
    }

    private static async Task<IResult> GetEnvironmentAsync(
        Guid organizationId,
        Guid projectId,
        Guid environmentId,
        CurrentUserAccessor currentUserAccessor,
        TenantAccessService tenantAccess,
        DevControlDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var actor = await currentUserAccessor.GetOrCreateAsync(cancellationToken);
        var access = await tenantAccess.RequireAsync(organizationId, actor, OrganizationRole.Viewer, cancellationToken);
        var failure = AccessFailure(access);
        if (failure is not null)
        {
            return failure;
        }

        var environment = await dbContext.ProjectEnvironments
            .SingleOrDefaultAsync(candidate =>
                    candidate.OrganizationId == organizationId &&
                    candidate.ProjectId == projectId &&
                    candidate.Id == environmentId,
                cancellationToken);
        return environment is null ? Results.NotFound() : Results.Ok(ToEnvironmentResponse(environment));
    }

    private static async Task<IResult> UpdateEnvironmentAsync(
        Guid organizationId,
        Guid projectId,
        Guid environmentId,
        EnvironmentUpsertRequest request,
        CurrentUserAccessor currentUserAccessor,
        TenantAccessService tenantAccess,
        DevControlDbContext dbContext,
        AuditLogWriter auditLogWriter,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var actor = await currentUserAccessor.GetOrCreateAsync(cancellationToken);
        var access = await tenantAccess.RequireAsync(
            organizationId,
            actor,
            OrganizationRole.Developer,
            cancellationToken,
            auditDenied: true,
            deniedAction: "environment.update.denied",
            targetType: "environment",
            targetId: environmentId.ToString());
        var failure = AccessFailure(access);
        if (failure is not null)
        {
            return failure;
        }

        var environment = await dbContext.ProjectEnvironments
            .SingleOrDefaultAsync(candidate =>
                    candidate.OrganizationId == organizationId &&
                    candidate.ProjectId == projectId &&
                    candidate.Id == environmentId,
                cancellationToken);
        if (environment is null)
        {
            return Results.NotFound();
        }

        var validation = NormalizeNameAndSlug(request.Name, request.Slug, out var name, out var slug);
        if (validation is not null)
        {
            return validation;
        }

        if (await dbContext.ProjectEnvironments.AnyAsync(
                candidate => candidate.ProjectId == projectId && candidate.Id != environmentId && candidate.Slug == slug,
                cancellationToken))
        {
            return Conflict("Environment slug is already in use for this project.");
        }

        environment.Update(name, slug, timeProvider.GetUtcNow());
        auditLogWriter.Add(
            organizationId,
            actor,
            "environment.update",
            "Succeeded",
            "environment",
            environment.Id.ToString(),
            "Environment updated.",
            new { environment.Name, environment.Slug },
            projectId,
            environment.Id);

        await dbContext.SaveChangesAsync(cancellationToken);
        return Results.Ok(ToEnvironmentResponse(environment));
    }

    private static async Task<IResult> ListAuditLogsAsync(
        Guid organizationId,
        CurrentUserAccessor currentUserAccessor,
        TenantAccessService tenantAccess,
        DevControlDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var actor = await currentUserAccessor.GetOrCreateAsync(cancellationToken);
        var access = await tenantAccess.RequireAsync(organizationId, actor, OrganizationRole.Admin, cancellationToken);
        var failure = AccessFailure(access);
        if (failure is not null)
        {
            return failure;
        }

        var auditLogs = await dbContext.AuditLogs
            .Where(auditLog => auditLog.OrganizationId == organizationId)
            .OrderByDescending(auditLog => auditLog.CreatedAt)
            .Take(100)
            .Select(auditLog => new AuditLogResponse(
                auditLog.Id,
                auditLog.ActorEmail,
                auditLog.Action,
                auditLog.Outcome,
                auditLog.TargetType,
                auditLog.TargetId,
                auditLog.Message,
                auditLog.CreatedAt))
            .ToListAsync(cancellationToken);

        return Results.Ok(auditLogs);
    }

    private static async Task<IResult> ListControlActionsAsync(
        Guid organizationId,
        CurrentUserAccessor currentUserAccessor,
        TenantAccessService tenantAccess,
        DevControlDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var actor = await currentUserAccessor.GetOrCreateAsync(cancellationToken);
        var access = await tenantAccess.RequireAsync(organizationId, actor, OrganizationRole.Developer, cancellationToken);
        var failure = AccessFailure(access);
        if (failure is not null)
        {
            return failure;
        }

        var controlActions = await dbContext.ControlActions
            .Where(controlAction => controlAction.OrganizationId == organizationId)
            .OrderByDescending(controlAction => controlAction.RequestedAt)
            .Take(100)
            .Select(controlAction => new ControlActionResponse(
                controlAction.Id,
                controlAction.ProjectId,
                controlAction.EnvironmentId,
                controlAction.ActionType,
                controlAction.Status.ToString(),
                controlAction.TargetType,
                controlAction.TargetId,
                controlAction.CorrelationId,
                controlAction.RequestedAt,
                controlAction.CompletedAt))
            .ToListAsync(cancellationToken);

        return Results.Ok(controlActions);
    }

    private static IResult? AccessFailure(TenantAccessResult result)
    {
        return result.Status switch
        {
            TenantAccessStatus.Granted => null,
            TenantAccessStatus.Forbidden => Results.Forbid(),
            _ => Results.NotFound()
        };
    }

    private static IResult? NormalizeNameAndSlug(string? requestName, string? requestSlug, out string name, out string slug)
    {
        name = string.Empty;
        slug = string.Empty;

        if (string.IsNullOrWhiteSpace(requestName))
        {
            return Results.BadRequest(new ProblemDetailsResponse("Name is required."));
        }

        try
        {
            name = requestName.Trim();
            slug = SlugNormalizer.Normalize(string.IsNullOrWhiteSpace(requestSlug) ? name : requestSlug);
            return null;
        }
        catch (ArgumentException exception)
        {
            return Results.BadRequest(new ProblemDetailsResponse(exception.Message));
        }
    }

    private static bool TryParseRole(string? rawRole, out OrganizationRole role, out IResult failure)
    {
        if (Enum.TryParse(rawRole, ignoreCase: true, out role) && Enum.IsDefined(role))
        {
            failure = Results.Empty;
            return true;
        }

        failure = Results.BadRequest(new ProblemDetailsResponse("Role must be Owner, Admin, Developer, or Viewer."));
        return false;
    }

    private static string NormalizeInvitationEmail(string? email, out IResult? failure)
    {
        failure = null;
        try
        {
            if (string.IsNullOrWhiteSpace(email))
            {
                failure = Results.BadRequest(new ProblemDetailsResponse("Email is required."));
                return string.Empty;
            }

            return EmailAddressNormalizer.Normalize(email);
        }
        catch (ArgumentException exception)
        {
            failure = Results.BadRequest(new ProblemDetailsResponse(exception.Message));
            return string.Empty;
        }
    }

    private static async Task<bool> ProjectExistsAsync(
        DevControlDbContext dbContext,
        Guid organizationId,
        Guid projectId,
        CancellationToken cancellationToken)
    {
        return await dbContext.Projects.AnyAsync(
            project => project.OrganizationId == organizationId && project.Id == projectId,
            cancellationToken);
    }

    private static IResult Conflict(string detail)
    {
        return Results.Problem(detail, statusCode: StatusCodes.Status409Conflict);
    }

    private static void AddDeniedAudit(
        AuditLogWriter auditLogWriter,
        Guid organizationId,
        CurrentUser actor,
        string action,
        string targetType,
        string? targetId,
        string message)
    {
        auditLogWriter.Add(
            organizationId,
            actor,
            action,
            "Denied",
            targetType,
            targetId,
            message);
    }

    private static async Task SendInvitationEmailAsync(
        HttpContext httpContext,
        IEmailSender emailSender,
        ILoggerFactory loggerFactory,
        string organizationName,
        OrganizationInvitation invitation,
        string token,
        CancellationToken cancellationToken)
    {
        var logger = loggerFactory.CreateLogger("DevControl.InvitationEmail");
        var origin = $"{httpContext.Request.Scheme}://{httpContext.Request.Host}";
        var acceptUrl = $"{origin}/invitations/{WebUtility.UrlEncode(token)}";
        var escapedOrganization = WebUtility.HtmlEncode(organizationName);
        var escapedRole = WebUtility.HtmlEncode(invitation.Role.ToString());
        var escapedUrl = WebUtility.HtmlEncode(acceptUrl);

        var message = new EmailMessage(
            invitation.Email,
            $"Invitation to {organizationName} on DevControl",
            $"You were invited to {organizationName} as {invitation.Role}. Accept this invitation: {acceptUrl}",
            $"<p>You were invited to <strong>{escapedOrganization}</strong> as <strong>{escapedRole}</strong>.</p><p><a href=\"{escapedUrl}\">Accept invitation</a></p>");

        try
        {
            await emailSender.SendAsync(message, cancellationToken);
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Invitation email delivery failed for invitation {InvitationId}.", invitation.Id);
        }
    }

    private static InvitationResponse ToInvitationResponse(OrganizationInvitation invitation)
    {
        return new InvitationResponse(
            invitation.Id,
            invitation.Email,
            invitation.Role.ToString(),
            invitation.Status.ToString(),
            invitation.ExpiresAt,
            invitation.LastSentAt,
            invitation.AcceptedAt,
            invitation.RevokedAt);
    }

    private static ProjectResponse ToProjectResponse(Project project)
    {
        return new ProjectResponse(
            project.Id,
            project.OrganizationId,
            project.Name,
            project.Slug,
            project.Description,
            project.CreatedAt,
            project.UpdatedAt);
    }

    private static EnvironmentResponse ToEnvironmentResponse(ProjectEnvironment environment)
    {
        return new EnvironmentResponse(
            environment.Id,
            environment.ProjectId,
            environment.Name,
            environment.Slug,
            environment.CreatedAt,
            environment.UpdatedAt);
    }
}

public sealed record ProblemDetailsResponse(string Detail);

public sealed record OrganizationUpsertRequest(string? Name, string? Slug);

public sealed record OrganizationResponse(
    Guid Id,
    string Name,
    string Slug,
    string Role,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record MemberUpdateRequest(string? Role);

public sealed record MemberResponse(
    Guid Id,
    Guid UserId,
    string Email,
    string DisplayName,
    string Role,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record MemberRoleResponse(Guid Id, string Role);

public sealed record InvitationCreateRequest(string? Email, string? Role);

public sealed record InvitationResponse(
    Guid Id,
    string Email,
    string Role,
    string Status,
    DateTimeOffset ExpiresAt,
    DateTimeOffset LastSentAt,
    DateTimeOffset? AcceptedAt,
    DateTimeOffset? RevokedAt);

public sealed record ProjectUpsertRequest(string? Name, string? Slug, string? Description);

public sealed record ProjectResponse(
    Guid Id,
    Guid OrganizationId,
    string Name,
    string Slug,
    string Description,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record EnvironmentUpsertRequest(string? Name, string? Slug);

public sealed record EnvironmentResponse(
    Guid Id,
    Guid ProjectId,
    string Name,
    string Slug,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record AuditLogResponse(
    Guid Id,
    string ActorEmail,
    string Action,
    string Outcome,
    string TargetType,
    string? TargetId,
    string Message,
    DateTimeOffset CreatedAt);

public sealed record ControlActionResponse(
    Guid Id,
    Guid? ProjectId,
    Guid? EnvironmentId,
    string ActionType,
    string Status,
    string TargetType,
    string? TargetId,
    string? CorrelationId,
    DateTimeOffset RequestedAt,
    DateTimeOffset? CompletedAt);
