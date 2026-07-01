using System.Text.Json;
using DevControl.Api.Security;
using DevControl.Api.Webhooks;
using DevControl.Application.Outbound;
using DevControl.Application.Security;
using DevControl.Application.Webhooks;
using DevControl.Domain.Entities;
using DevControl.Domain.Enums;
using DevControl.Infrastructure.Database;
using DevControl.Infrastructure.Outbound;
using Microsoft.EntityFrameworkCore;

namespace DevControl.Api.Endpoints;

public static class MonitoringEndpoints
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static void MapMonitoringEndpoints(this WebApplication app)
    {
        app.MapGet("/api/public/status/{organizationSlug}/{projectSlug}", GetPublicStatusAsync);

        var api = app.MapGroup("/api").RequireAuthorization();

        api.MapGet("/organizations/{organizationId:guid}/monitors", ListMonitorsAsync);
        api.MapPatch("/organizations/{organizationId:guid}/monitors/{monitorId:guid}", UpdateMonitorAsync).RequireCsrf();
        api.MapPost("/organizations/{organizationId:guid}/monitors/{monitorId:guid}/pause", PauseMonitorAsync).RequireCsrf();
        api.MapPost("/organizations/{organizationId:guid}/monitors/{monitorId:guid}/resume", ResumeMonitorAsync).RequireCsrf();
        api.MapGet("/organizations/{organizationId:guid}/monitors/{monitorId:guid}/checks", ListMonitorChecksAsync);

        api.MapGet("/organizations/{organizationId:guid}/incidents", ListIncidentsAsync);
        api.MapPost("/organizations/{organizationId:guid}/projects/{projectId:guid}/environments/{environmentId:guid}/incidents", CreateIncidentAsync).RequireCsrf();
        api.MapPatch("/organizations/{organizationId:guid}/incidents/{incidentId:guid}", UpdateIncidentAsync).RequireCsrf();
        api.MapGet("/organizations/{organizationId:guid}/incidents/{incidentId:guid}/updates", ListIncidentUpdatesAsync);
        api.MapPost("/organizations/{organizationId:guid}/incidents/{incidentId:guid}/updates", AddIncidentUpdateAsync).RequireCsrf();

        api.MapGet("/organizations/{organizationId:guid}/releases", ListReleasesAsync);
        api.MapPost("/organizations/{organizationId:guid}/projects/{projectId:guid}/environments/{environmentId:guid}/releases", CreateReleaseAsync).RequireCsrf();
        api.MapPatch("/organizations/{organizationId:guid}/releases/{releaseId:guid}", UpdateReleaseAsync).RequireCsrf();
        api.MapPost("/organizations/{organizationId:guid}/releases/{releaseId:guid}/publish", PublishReleaseAsync).RequireCsrf();
    }

    private static async Task<IResult> ListMonitorsAsync(
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

        var monitors = await QueryMonitorResponses(dbContext, organizationId, orderByScope: true)
            .ToListAsync(cancellationToken);

        return Results.Ok(monitors);
    }

    private static async Task<IResult> UpdateMonitorAsync(
        Guid organizationId,
        Guid monitorId,
        MonitorUpdateRequest request,
        CurrentUserAccessor currentUserAccessor,
        TenantAccessService tenantAccess,
        DevControlDbContext dbContext,
        AuditLogWriter auditLogWriter,
        OutboundRequestGuard outboundRequestGuard,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var actor = await currentUserAccessor.GetOrCreateAsync(cancellationToken);
        var monitor = await dbContext.UptimeMonitors
            .SingleOrDefaultAsync(candidate => candidate.OrganizationId == organizationId && candidate.Id == monitorId, cancellationToken);
        if (monitor is null)
        {
            return Results.NotFound();
        }

        var environment = await dbContext.ProjectEnvironments
            .SingleAsync(candidate => candidate.OrganizationId == organizationId && candidate.Id == monitor.EnvironmentId, cancellationToken);
        var accessFailure = await RequireMonitorMutationAsync(
            organizationId,
            actor,
            environment,
            tenantAccess,
            auditLogWriter,
            dbContext,
            "monitor.update.denied",
            monitor.Id.ToString(),
            cancellationToken);
        if (accessFailure is not null)
        {
            return accessFailure;
        }

        var validation = await ValidateMonitorRequestAsync(request, outboundRequestGuard, cancellationToken);
        if (validation.Failure is not null)
        {
            return validation.Failure;
        }

        var now = timeProvider.GetUtcNow();
        try
        {
            monitor.UpdateSettings(
                validation.Name,
                validation.Url,
                validation.IntervalSeconds,
                validation.TimeoutSeconds,
                validation.SlowThresholdMilliseconds,
                validation.FailureThreshold,
                validation.RecoveryThreshold,
                actor.Id,
                now);
        }
        catch (ArgumentOutOfRangeException exception)
        {
            return Results.BadRequest(new ProblemDetailsResponse(exception.Message));
        }
        catch (ArgumentException exception)
        {
            return Results.BadRequest(new ProblemDetailsResponse(exception.Message));
        }

        AddCompletedControlAction(dbContext, organizationId, monitor.ProjectId, monitor.EnvironmentId, actor, "monitor.update", "uptime_monitor", monitor.Id.ToString(), request, new { monitor.Id, monitor.Name, monitor.Url }, now);
        auditLogWriter.Add(
            organizationId,
            actor,
            "monitor.update",
            "Succeeded",
            "uptime_monitor",
            monitor.Id.ToString(),
            "Uptime monitor updated.",
            new { monitor.Name, monitor.Url },
            monitor.ProjectId,
            monitor.EnvironmentId);

        await dbContext.SaveChangesAsync(cancellationToken);
        var response = await QueryMonitorResponses(dbContext, organizationId, monitor.Id)
            .SingleAsync(cancellationToken);
        return Results.Ok(response);
    }

    private static async Task<IResult> PauseMonitorAsync(
        Guid organizationId,
        Guid monitorId,
        CurrentUserAccessor currentUserAccessor,
        TenantAccessService tenantAccess,
        DevControlDbContext dbContext,
        AuditLogWriter auditLogWriter,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        return await ChangeMonitorPauseAsync(organizationId, monitorId, paused: true, currentUserAccessor, tenantAccess, dbContext, auditLogWriter, timeProvider, cancellationToken);
    }

    private static async Task<IResult> ResumeMonitorAsync(
        Guid organizationId,
        Guid monitorId,
        CurrentUserAccessor currentUserAccessor,
        TenantAccessService tenantAccess,
        DevControlDbContext dbContext,
        AuditLogWriter auditLogWriter,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        return await ChangeMonitorPauseAsync(organizationId, monitorId, paused: false, currentUserAccessor, tenantAccess, dbContext, auditLogWriter, timeProvider, cancellationToken);
    }

    private static async Task<IResult> ListMonitorChecksAsync(
        Guid organizationId,
        Guid monitorId,
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

        if (!await dbContext.UptimeMonitors.AnyAsync(monitor => monitor.OrganizationId == organizationId && monitor.Id == monitorId, cancellationToken))
        {
            return Results.NotFound();
        }

        var checks = await dbContext.MonitorChecks
            .Where(check => check.OrganizationId == organizationId && check.UptimeMonitorId == monitorId)
            .OrderByDescending(check => check.CheckedAt)
            .Take(50)
            .Select(check => new MonitorCheckResponse(
                check.Id,
                check.UptimeMonitorId,
                check.Status.ToString(),
                check.Succeeded,
                check.StatusCode,
                check.ResultKind,
                check.DurationMilliseconds,
                check.Error,
                check.ResponsePreview,
                check.ResponseTruncated,
                check.CheckedAt))
            .ToListAsync(cancellationToken);

        return Results.Ok(checks);
    }

    private static async Task<IResult> ListIncidentsAsync(
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

        var incidents = await QueryIncidentResponses(dbContext, organizationId, latestFirst: true)
            .Take(50)
            .ToListAsync(cancellationToken);

        return Results.Ok(incidents);
    }

    private static async Task<IResult> CreateIncidentAsync(
        Guid organizationId,
        Guid projectId,
        Guid environmentId,
        IncidentCreateRequest request,
        CurrentUserAccessor currentUserAccessor,
        TenantAccessService tenantAccess,
        DevControlDbContext dbContext,
        AuditLogWriter auditLogWriter,
        WebhookEventPublisher webhookEventPublisher,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var actor = await currentUserAccessor.GetOrCreateAsync(cancellationToken);
        var scope = await LoadScopedEnvironmentAsync(dbContext, organizationId, projectId, environmentId, cancellationToken);
        if (scope is null)
        {
            return Results.NotFound();
        }

        var failure = await RequireRoleAsync(organizationId, actor, OrganizationRole.Developer, tenantAccess, "incident.create.denied", "incident", null, cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        var now = timeProvider.GetUtcNow();
        Incident incident;
        try
        {
            incident = new Incident(organizationId, projectId, environmentId, request.Title ?? string.Empty, request.Summary ?? string.Empty, actor.Id, now);
        }
        catch (ArgumentException exception)
        {
            return Results.BadRequest(new ProblemDetailsResponse(exception.Message));
        }

        dbContext.Incidents.Add(incident);
        dbContext.IncidentUpdates.Add(new IncidentUpdate(
            incident,
            incident.Status,
            request.Private ? IncidentUpdateVisibility.Private : IncidentUpdateVisibility.Public,
            string.IsNullOrWhiteSpace(request.Message)
                ? string.IsNullOrWhiteSpace(incident.Summary) ? "Incident created." : incident.Summary
                : request.Message!,
            actor.Id,
            actor.Email,
            now));
        AddCompletedControlAction(dbContext, organizationId, projectId, environmentId, actor, "incident.create", "incident", incident.Id.ToString(), request, new { incident.Id, incident.Status }, now);
        auditLogWriter.Add(organizationId, actor, "incident.create", "Succeeded", "incident", incident.Id.ToString(), "Incident created.", new { incident.Title, incident.Status }, projectId, environmentId);
        await PublishIncidentEventAsync(webhookEventPublisher, WebhookEventTypes.IncidentCreated, incident, actor, now, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        return Results.Created($"/api/organizations/{organizationId}/incidents/{incident.Id}", ToIncidentResponse(incident, scope.Project, scope.Environment));
    }

    private static async Task<IResult> UpdateIncidentAsync(
        Guid organizationId,
        Guid incidentId,
        IncidentUpdateRequest request,
        CurrentUserAccessor currentUserAccessor,
        TenantAccessService tenantAccess,
        DevControlDbContext dbContext,
        AuditLogWriter auditLogWriter,
        WebhookEventPublisher webhookEventPublisher,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var actor = await currentUserAccessor.GetOrCreateAsync(cancellationToken);
        var incident = await dbContext.Incidents
            .SingleOrDefaultAsync(candidate => candidate.OrganizationId == organizationId && candidate.Id == incidentId, cancellationToken);
        if (incident is null)
        {
            return Results.NotFound();
        }

        var failure = await RequireRoleAsync(organizationId, actor, OrganizationRole.Developer, tenantAccess, "incident.update.denied", "incident", incident.Id.ToString(), cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        if (!TryParseIncidentStatus(request.Status ?? incident.Status.ToString(), out var status, out var statusError))
        {
            return Results.BadRequest(new ProblemDetailsResponse(statusError!));
        }

        var now = timeProvider.GetUtcNow();
        try
        {
            incident.Update(
                request.Title ?? incident.Title,
                request.Summary ?? incident.Summary,
                status,
                request.RootCauseSummary ?? incident.RootCauseSummary,
                request.PostmortemDraft ?? incident.PostmortemDraft,
                actor.Id,
                now);
        }
        catch (ArgumentException exception)
        {
            return Results.BadRequest(new ProblemDetailsResponse(exception.Message));
        }

        if (!string.IsNullOrWhiteSpace(request.Message))
        {
            dbContext.IncidentUpdates.Add(new IncidentUpdate(
                incident,
                incident.Status,
                request.Private ? IncidentUpdateVisibility.Private : IncidentUpdateVisibility.Public,
                request.Message,
                actor.Id,
                actor.Email,
                now));
        }

        AddCompletedControlAction(dbContext, organizationId, incident.ProjectId, incident.EnvironmentId, actor, "incident.update", "incident", incident.Id.ToString(), request, new { incident.Id, incident.Status }, now);
        auditLogWriter.Add(organizationId, actor, "incident.update", "Succeeded", "incident", incident.Id.ToString(), "Incident updated.", new { incident.Title, incident.Status }, incident.ProjectId, incident.EnvironmentId);
        await PublishIncidentEventAsync(webhookEventPublisher, incident.Status == IncidentStatus.Resolved ? WebhookEventTypes.IncidentResolved : WebhookEventTypes.IncidentUpdated, incident, actor, now, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        var response = await QueryIncidentResponses(dbContext, organizationId, incident.Id).SingleAsync(cancellationToken);
        return Results.Ok(response);
    }

    private static async Task<IResult> ListIncidentUpdatesAsync(
        Guid organizationId,
        Guid incidentId,
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

        if (!await dbContext.Incidents.AnyAsync(incident => incident.OrganizationId == organizationId && incident.Id == incidentId, cancellationToken))
        {
            return Results.NotFound();
        }

        var updates = await QueryIncidentUpdateResponses(dbContext, organizationId, incidentId, publicOnly: false, latestFirst: true)
            .ToListAsync(cancellationToken);
        return Results.Ok(updates);
    }

    private static async Task<IResult> AddIncidentUpdateAsync(
        Guid organizationId,
        Guid incidentId,
        IncidentTimelineUpdateRequest request,
        CurrentUserAccessor currentUserAccessor,
        TenantAccessService tenantAccess,
        DevControlDbContext dbContext,
        AuditLogWriter auditLogWriter,
        WebhookEventPublisher webhookEventPublisher,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var actor = await currentUserAccessor.GetOrCreateAsync(cancellationToken);
        var incident = await dbContext.Incidents
            .SingleOrDefaultAsync(candidate => candidate.OrganizationId == organizationId && candidate.Id == incidentId, cancellationToken);
        if (incident is null)
        {
            return Results.NotFound();
        }

        var failure = await RequireRoleAsync(organizationId, actor, OrganizationRole.Developer, tenantAccess, "incident_update.create.denied", "incident", incident.Id.ToString(), cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        if (!TryParseIncidentStatus(request.Status ?? incident.Status.ToString(), out var status, out var statusError))
        {
            return Results.BadRequest(new ProblemDetailsResponse(statusError!));
        }

        var message = request.Message?.Trim() ?? string.Empty;
        if (message.Length == 0)
        {
            return Results.BadRequest(new ProblemDetailsResponse("Incident update message is required."));
        }

        var now = timeProvider.GetUtcNow();
        incident.Update(incident.Title, incident.Summary, status, incident.RootCauseSummary, incident.PostmortemDraft, actor.Id, now);
        var update = new IncidentUpdate(
            incident,
            incident.Status,
            request.Private ? IncidentUpdateVisibility.Private : IncidentUpdateVisibility.Public,
            message,
            actor.Id,
            actor.Email,
            now);
        dbContext.IncidentUpdates.Add(update);
        AddCompletedControlAction(dbContext, organizationId, incident.ProjectId, incident.EnvironmentId, actor, "incident_update.create", "incident", incident.Id.ToString(), request, new { update.Id, incident.Status }, now);
        auditLogWriter.Add(organizationId, actor, "incident_update.create", "Succeeded", "incident", incident.Id.ToString(), "Incident update added.", new { incident.Title, incident.Status, update.Visibility }, incident.ProjectId, incident.EnvironmentId);
        await PublishIncidentEventAsync(webhookEventPublisher, incident.Status == IncidentStatus.Resolved ? WebhookEventTypes.IncidentResolved : WebhookEventTypes.IncidentUpdated, incident, actor, now, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        return Results.Created($"/api/organizations/{organizationId}/incidents/{incidentId}/updates/{update.Id}", ToIncidentUpdateResponse(update));
    }

    private static async Task<IResult> ListReleasesAsync(
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

        var releases = await QueryReleaseResponses(dbContext, organizationId, latestFirst: true)
            .Take(50)
            .ToListAsync(cancellationToken);
        return Results.Ok(releases);
    }

    private static async Task<IResult> CreateReleaseAsync(
        Guid organizationId,
        Guid projectId,
        Guid environmentId,
        ReleaseCreateRequest request,
        CurrentUserAccessor currentUserAccessor,
        TenantAccessService tenantAccess,
        DevControlDbContext dbContext,
        AuditLogWriter auditLogWriter,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var actor = await currentUserAccessor.GetOrCreateAsync(cancellationToken);
        var scope = await LoadScopedEnvironmentAsync(dbContext, organizationId, projectId, environmentId, cancellationToken);
        if (scope is null)
        {
            return Results.NotFound();
        }

        var failure = await RequireRoleAsync(organizationId, actor, OrganizationRole.Developer, tenantAccess, "release.create.denied", "status_release", null, cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        StatusRelease release;
        var now = timeProvider.GetUtcNow();
        try
        {
            release = new StatusRelease(organizationId, projectId, environmentId, request.Title ?? string.Empty, request.Version ?? string.Empty, request.Body ?? string.Empty, actor.Id, now);
        }
        catch (ArgumentException exception)
        {
            return Results.BadRequest(new ProblemDetailsResponse(exception.Message));
        }

        dbContext.StatusReleases.Add(release);
        AddCompletedControlAction(dbContext, organizationId, projectId, environmentId, actor, "release.create", "status_release", release.Id.ToString(), request, new { release.Id, release.Status }, now);
        auditLogWriter.Add(organizationId, actor, "release.create", "Succeeded", "status_release", release.Id.ToString(), "Release draft created.", new { release.Title, release.Version }, projectId, environmentId);
        await dbContext.SaveChangesAsync(cancellationToken);
        return Results.Created($"/api/organizations/{organizationId}/releases/{release.Id}", ToReleaseResponse(release, scope.Project, scope.Environment));
    }

    private static async Task<IResult> UpdateReleaseAsync(
        Guid organizationId,
        Guid releaseId,
        ReleaseCreateRequest request,
        CurrentUserAccessor currentUserAccessor,
        TenantAccessService tenantAccess,
        DevControlDbContext dbContext,
        AuditLogWriter auditLogWriter,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var actor = await currentUserAccessor.GetOrCreateAsync(cancellationToken);
        var release = await dbContext.StatusReleases
            .SingleOrDefaultAsync(candidate => candidate.OrganizationId == organizationId && candidate.Id == releaseId, cancellationToken);
        if (release is null)
        {
            return Results.NotFound();
        }

        var failure = await RequireRoleAsync(organizationId, actor, OrganizationRole.Developer, tenantAccess, "release.update.denied", "status_release", release.Id.ToString(), cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        var now = timeProvider.GetUtcNow();
        try
        {
            release.Update(request.Title ?? string.Empty, request.Version ?? string.Empty, request.Body ?? string.Empty, actor.Id, now);
        }
        catch (InvalidOperationException exception)
        {
            return Results.BadRequest(new ProblemDetailsResponse(exception.Message));
        }
        catch (ArgumentException exception)
        {
            return Results.BadRequest(new ProblemDetailsResponse(exception.Message));
        }

        AddCompletedControlAction(dbContext, organizationId, release.ProjectId, release.EnvironmentId, actor, "release.update", "status_release", release.Id.ToString(), request, new { release.Id, release.Status }, now);
        auditLogWriter.Add(organizationId, actor, "release.update", "Succeeded", "status_release", release.Id.ToString(), "Release draft updated.", new { release.Title, release.Version }, release.ProjectId, release.EnvironmentId);
        await dbContext.SaveChangesAsync(cancellationToken);
        var response = await QueryReleaseResponses(dbContext, organizationId, release.Id).SingleAsync(cancellationToken);
        return Results.Ok(response);
    }

    private static async Task<IResult> PublishReleaseAsync(
        Guid organizationId,
        Guid releaseId,
        CurrentUserAccessor currentUserAccessor,
        TenantAccessService tenantAccess,
        DevControlDbContext dbContext,
        AuditLogWriter auditLogWriter,
        WebhookEventPublisher webhookEventPublisher,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var actor = await currentUserAccessor.GetOrCreateAsync(cancellationToken);
        var release = await dbContext.StatusReleases
            .SingleOrDefaultAsync(candidate => candidate.OrganizationId == organizationId && candidate.Id == releaseId, cancellationToken);
        if (release is null)
        {
            return Results.NotFound();
        }

        var failure = await RequireRoleAsync(organizationId, actor, OrganizationRole.Admin, tenantAccess, "release.publish.denied", "status_release", release.Id.ToString(), cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        var now = timeProvider.GetUtcNow();
        release.Publish(actor.Id, now);
        AddCompletedControlAction(dbContext, organizationId, release.ProjectId, release.EnvironmentId, actor, "release.publish", "status_release", release.Id.ToString(), new { release.Id }, new { release.Id, release.Status, release.PublishedAt }, now);
        auditLogWriter.Add(organizationId, actor, "release.publish", "Succeeded", "status_release", release.Id.ToString(), "Release published.", new { release.Title, release.Version }, release.ProjectId, release.EnvironmentId);
        await webhookEventPublisher.PublishAsync(
            organizationId,
            release.ProjectId,
            release.EnvironmentId,
            WebhookEventTypes.ReleasePublished,
            "status_release",
            release.Id.ToString(),
            actor.Id,
            actor.Email,
            new { release.Id, release.Title, release.Version, release.PublishedAt },
            now,
            cancellationToken);

        await dbContext.SaveChangesAsync(cancellationToken);
        var response = await QueryReleaseResponses(dbContext, organizationId, release.Id).SingleAsync(cancellationToken);
        return Results.Ok(response);
    }

    private static async Task<IResult> GetPublicStatusAsync(
        string organizationSlug,
        string projectSlug,
        string? environment,
        DevControlDbContext dbContext,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var organization = await dbContext.Organizations.SingleOrDefaultAsync(candidate => candidate.Slug == organizationSlug, cancellationToken);
        if (organization is null)
        {
            return Results.NotFound();
        }

        var project = await dbContext.Projects.SingleOrDefaultAsync(candidate => candidate.OrganizationId == organization.Id && candidate.Slug == projectSlug, cancellationToken);
        if (project is null)
        {
            return Results.NotFound();
        }

        var environments = dbContext.ProjectEnvironments
            .Where(candidate => candidate.OrganizationId == organization.Id && candidate.ProjectId == project.Id);
        if (!string.IsNullOrWhiteSpace(environment))
        {
            environments = environments.Where(candidate => candidate.Slug == environment);
        }

        var environmentRows = await environments.OrderBy(candidate => candidate.Name).ToListAsync(cancellationToken);
        if (environmentRows.Count == 0)
        {
            return Results.NotFound();
        }

        var environmentIds = environmentRows.Select(candidate => candidate.Id).ToArray();
        var monitors = await dbContext.UptimeMonitors
            .Where(monitor => monitor.OrganizationId == organization.Id && monitor.ProjectId == project.Id && environmentIds.Contains(monitor.EnvironmentId))
            .Join(
                dbContext.ProjectEnvironments,
                monitor => monitor.EnvironmentId,
                env => env.Id,
                (monitor, env) => new { monitor, env })
            .OrderBy(candidate => candidate.env.Name)
            .ThenBy(candidate => candidate.monitor.Name)
            .ToListAsync(cancellationToken);

        var since = timeProvider.GetUtcNow().AddHours(-24);
        var publicMonitors = new List<PublicMonitorStatusResponse>();
        foreach (var candidate in monitors)
        {
            var checks = await dbContext.MonitorChecks
                .Where(check => check.UptimeMonitorId == candidate.monitor.Id && check.CheckedAt >= since)
                .ToListAsync(cancellationToken);
            var successful = checks.Count(check => check.Succeeded);
            var uptimePercent = checks.Count == 0 ? 100 : Math.Round(successful * 100.0 / checks.Count, 2);
            publicMonitors.Add(new PublicMonitorStatusResponse(
                candidate.monitor.Id,
                candidate.monitor.Name,
                candidate.env.Name,
                candidate.env.Slug,
                candidate.monitor.CurrentStatus.ToString(),
                candidate.monitor.LastCheckedAt,
                candidate.monitor.LastSuccessAt,
                candidate.monitor.LastFailureAt,
                checks.Count,
                uptimePercent));
        }

        var incidentRows = await QueryPublicIncidents(dbContext, organization.Id, project.Id, environmentIds, cancellationToken);
        var releases = await dbContext.StatusReleases
            .Where(release =>
                release.OrganizationId == organization.Id &&
                release.ProjectId == project.Id &&
                environmentIds.Contains(release.EnvironmentId) &&
                release.Status == ReleaseStatus.Published)
            .Join(
                dbContext.ProjectEnvironments,
                release => release.EnvironmentId,
                env => env.Id,
                (release, env) => new
                {
                    release.Id,
                    release.Title,
                    release.Version,
                    release.Body,
                    EnvironmentName = env.Name,
                    EnvironmentSlug = env.Slug,
                    release.PublishedAt
                })
            .OrderByDescending(release => release.PublishedAt)
            .Take(20)
            .Select(release => new PublicReleaseResponse(
                release.Id,
                release.Title,
                release.Version,
                release.Body,
                release.EnvironmentName,
                release.EnvironmentSlug,
                release.PublishedAt!.Value))
            .ToListAsync(cancellationToken);

        var overall = publicMonitors.Any(monitor => monitor.Status == MonitorStatus.Down.ToString())
            ? "down"
            : publicMonitors.Any(monitor => monitor.Status == MonitorStatus.Slow.ToString() || monitor.Status == MonitorStatus.Unknown.ToString())
                ? "degraded"
                : "operational";

        return Results.Ok(new PublicStatusPageResponse(
            organization.Name,
            organization.Slug,
            project.Name,
            project.Slug,
            overall,
            environmentRows.Select(env => new PublicEnvironmentResponse(env.Name, env.Slug)).ToArray(),
            publicMonitors,
            incidentRows,
            releases));
    }

    private static async Task<List<PublicIncidentResponse>> QueryPublicIncidents(
        DevControlDbContext dbContext,
        Guid organizationId,
        Guid projectId,
        IReadOnlyCollection<Guid> environmentIds,
        CancellationToken cancellationToken)
    {
        var incidents = await dbContext.Incidents
            .Where(incident =>
                incident.OrganizationId == organizationId &&
                incident.ProjectId == projectId &&
                environmentIds.Contains(incident.EnvironmentId))
            .Join(
                dbContext.ProjectEnvironments,
                incident => incident.EnvironmentId,
                env => env.Id,
                (incident, env) => new { incident, env })
            .OrderByDescending(candidate => candidate.incident.CreatedAt)
            .Take(20)
            .ToListAsync(cancellationToken);

        var result = new List<PublicIncidentResponse>();
        foreach (var row in incidents)
        {
            var updates = await QueryIncidentUpdateResponses(dbContext, organizationId, row.incident.Id, publicOnly: true, oldestFirst: true)
                .ToListAsync(cancellationToken);
            result.Add(new PublicIncidentResponse(
                row.incident.Id,
                row.incident.Title,
                row.incident.Status.ToString(),
                row.incident.Summary,
                row.env.Name,
                row.env.Slug,
                row.incident.CreatedAt,
                row.incident.ResolvedAt,
                updates));
        }

        return result;
    }

    private static async Task<IResult> ChangeMonitorPauseAsync(
        Guid organizationId,
        Guid monitorId,
        bool paused,
        CurrentUserAccessor currentUserAccessor,
        TenantAccessService tenantAccess,
        DevControlDbContext dbContext,
        AuditLogWriter auditLogWriter,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var actor = await currentUserAccessor.GetOrCreateAsync(cancellationToken);
        var monitor = await dbContext.UptimeMonitors
            .SingleOrDefaultAsync(candidate => candidate.OrganizationId == organizationId && candidate.Id == monitorId, cancellationToken);
        if (monitor is null)
        {
            return Results.NotFound();
        }

        var environment = await dbContext.ProjectEnvironments
            .SingleAsync(candidate => candidate.OrganizationId == organizationId && candidate.Id == monitor.EnvironmentId, cancellationToken);
        var accessFailure = await RequireMonitorMutationAsync(
            organizationId,
            actor,
            environment,
            tenantAccess,
            auditLogWriter,
            dbContext,
            paused ? "monitor.pause.denied" : "monitor.resume.denied",
            monitor.Id.ToString(),
            cancellationToken);
        if (accessFailure is not null)
        {
            return accessFailure;
        }

        var now = timeProvider.GetUtcNow();
        if (paused)
        {
            monitor.Pause(actor.Id, now);
        }
        else
        {
            monitor.Resume(actor.Id, now);
        }

        var action = paused ? "monitor.pause" : "monitor.resume";
        AddCompletedControlAction(dbContext, organizationId, monitor.ProjectId, monitor.EnvironmentId, actor, action, "uptime_monitor", monitor.Id.ToString(), new { monitor.Id }, new { monitor.Id, monitor.IsPaused }, now);
        auditLogWriter.Add(organizationId, actor, action, "Succeeded", "uptime_monitor", monitor.Id.ToString(), paused ? "Uptime monitor paused." : "Uptime monitor resumed.", new { monitor.Name, monitor.Url }, monitor.ProjectId, monitor.EnvironmentId);
        await dbContext.SaveChangesAsync(cancellationToken);

        var response = await QueryMonitorResponses(dbContext, organizationId, monitor.Id)
            .SingleAsync(cancellationToken);
        return Results.Ok(response);
    }

    private static async Task<IResult?> RequireMonitorMutationAsync(
        Guid organizationId,
        CurrentUser actor,
        ProjectEnvironment environment,
        TenantAccessService tenantAccess,
        AuditLogWriter auditLogWriter,
        DevControlDbContext dbContext,
        string deniedAction,
        string? targetId,
        CancellationToken cancellationToken)
    {
        var requiredRole = IsProduction(environment) ? OrganizationRole.Admin : OrganizationRole.Developer;
        var access = await tenantAccess.RequireAsync(organizationId, actor, OrganizationRole.Viewer, cancellationToken);
        var failure = AccessFailure(access);
        if (failure is not null)
        {
            return failure;
        }

        if (RolePermissions.AtLeast(access.Access!.Member.Role, requiredRole))
        {
            return null;
        }

        auditLogWriter.Add(
            organizationId,
            actor,
            deniedAction,
            "Denied",
            "uptime_monitor",
            targetId,
            $"Denied monitor mutation because {access.Access.Member.Role} is below required role {requiredRole}.",
            new { access.Access.Member.Role, requiredRole, environmentSlug = environment.Slug },
            environment.ProjectId,
            environment.Id);
        await dbContext.SaveChangesAsync(cancellationToken);
        return Results.Forbid();
    }

    private static async Task<IResult?> RequireRoleAsync(
        Guid organizationId,
        CurrentUser actor,
        OrganizationRole requiredRole,
        TenantAccessService tenantAccess,
        string deniedAction,
        string targetType,
        string? targetId,
        CancellationToken cancellationToken)
    {
        var access = await tenantAccess.RequireAsync(
            organizationId,
            actor,
            requiredRole,
            cancellationToken,
            auditDenied: true,
            deniedAction: deniedAction,
            targetType: targetType,
            targetId: targetId);
        return AccessFailure(access);
    }

    private static async Task<NormalizedMonitorRequest> ValidateMonitorRequestAsync(
        MonitorUpdateRequest request,
        OutboundRequestGuard outboundRequestGuard,
        CancellationToken cancellationToken)
    {
        var name = request.Name?.Trim() ?? string.Empty;
        if (name.Length == 0)
        {
            return NormalizedMonitorRequest.Failed(Results.BadRequest(new ProblemDetailsResponse("Monitor name is required.")));
        }

        if (name.Length > 160)
        {
            return NormalizedMonitorRequest.Failed(Results.BadRequest(new ProblemDetailsResponse("Monitor name cannot exceed 160 characters.")));
        }

        var url = request.Url?.Trim() ?? string.Empty;
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            return NormalizedMonitorRequest.Failed(Results.BadRequest(new ProblemDetailsResponse("Monitor URL must be absolute.")));
        }

        var guardResult = await outboundRequestGuard.ValidateAsync(uri, OutboundRequestPolicy.Monitor, cancellationToken);
        if (!guardResult.IsAllowed)
        {
            return NormalizedMonitorRequest.Failed(Results.BadRequest(new ProblemDetailsResponse(guardResult.Error ?? "Monitor URL is not allowed.")));
        }

        return NormalizedMonitorRequest.Valid(
            name,
            uri.ToString(),
            request.IntervalSeconds ?? UptimeMonitor.DefaultIntervalSeconds,
            request.TimeoutSeconds ?? UptimeMonitor.DefaultTimeoutSeconds,
            request.SlowThresholdMilliseconds ?? UptimeMonitor.DefaultSlowThresholdMilliseconds,
            request.FailureThreshold ?? UptimeMonitor.DefaultFailureThreshold,
            request.RecoveryThreshold ?? UptimeMonitor.DefaultRecoveryThreshold);
    }

    private static IQueryable<MonitorResponse> QueryMonitorResponses(
        DevControlDbContext dbContext,
        Guid organizationId,
        Guid? monitorId = null,
        bool orderByScope = false)
    {
        var monitors = dbContext.UptimeMonitors
            .Where(monitor => monitor.OrganizationId == organizationId);
        if (monitorId is { } id)
        {
            monitors = monitors.Where(monitor => monitor.Id == id);
        }

        var query = monitors
            .Join(dbContext.Projects, monitor => monitor.ProjectId, project => project.Id, (monitor, project) => new { monitor, project })
            .Join(dbContext.ProjectEnvironments, candidate => candidate.monitor.EnvironmentId, environment => environment.Id, (candidate, environment) => new
            {
                candidate.monitor.Id,
                candidate.monitor.LiveAppId,
                candidate.monitor.Name,
                candidate.monitor.Url,
                candidate.monitor.IsManagedFromLiveApp,
                candidate.monitor.IsPaused,
                candidate.monitor.CurrentStatus,
                candidate.monitor.IntervalSeconds,
                candidate.monitor.TimeoutSeconds,
                candidate.monitor.SlowThresholdMilliseconds,
                candidate.monitor.FailureThreshold,
                candidate.monitor.RecoveryThreshold,
                candidate.monitor.ConsecutiveFailures,
                candidate.monitor.ConsecutiveRecoveries,
                candidate.monitor.ProjectId,
                ProjectName = candidate.project.Name,
                ProjectSlug = candidate.project.Slug,
                candidate.monitor.EnvironmentId,
                EnvironmentName = environment.Name,
                EnvironmentSlug = environment.Slug,
                candidate.monitor.NextCheckAt,
                candidate.monitor.LastCheckedAt,
                candidate.monitor.LastSuccessAt,
                candidate.monitor.LastFailureAt,
                candidate.monitor.CreatedAt,
                candidate.monitor.UpdatedAt
            });

        if (orderByScope)
        {
            query = query
                .OrderBy(monitor => monitor.ProjectName)
                .ThenBy(monitor => monitor.EnvironmentName)
                .ThenBy(monitor => monitor.Name);
        }

        return query.Select(monitor => new MonitorResponse(
            monitor.Id,
            monitor.LiveAppId,
            monitor.Name,
            monitor.Url,
            monitor.IsManagedFromLiveApp,
            monitor.IsPaused,
            monitor.CurrentStatus.ToString(),
            monitor.IntervalSeconds,
            monitor.TimeoutSeconds,
            monitor.SlowThresholdMilliseconds,
            monitor.FailureThreshold,
            monitor.RecoveryThreshold,
            monitor.ConsecutiveFailures,
            monitor.ConsecutiveRecoveries,
            monitor.ProjectId,
            monitor.ProjectName,
            monitor.ProjectSlug,
            monitor.EnvironmentId,
            monitor.EnvironmentName,
            monitor.EnvironmentSlug,
            monitor.NextCheckAt,
            monitor.LastCheckedAt,
            monitor.LastSuccessAt,
            monitor.LastFailureAt,
            monitor.CreatedAt,
            monitor.UpdatedAt));
    }

    private static IQueryable<IncidentResponse> QueryIncidentResponses(
        DevControlDbContext dbContext,
        Guid organizationId,
        Guid? incidentId = null,
        bool latestFirst = false)
    {
        var incidents = dbContext.Incidents
            .Where(incident => incident.OrganizationId == organizationId);
        if (incidentId is { } id)
        {
            incidents = incidents.Where(incident => incident.Id == id);
        }

        var query = incidents
            .Join(dbContext.Projects, incident => incident.ProjectId, project => project.Id, (incident, project) => new { incident, project })
            .Join(dbContext.ProjectEnvironments, candidate => candidate.incident.EnvironmentId, environment => environment.Id, (candidate, environment) => new
            {
                candidate.incident.Id,
                candidate.incident.Title,
                candidate.incident.Status,
                candidate.incident.Summary,
                candidate.incident.RootCauseSummary,
                candidate.incident.PostmortemDraft,
                candidate.incident.ProjectId,
                ProjectName = candidate.project.Name,
                ProjectSlug = candidate.project.Slug,
                candidate.incident.EnvironmentId,
                EnvironmentName = environment.Name,
                EnvironmentSlug = environment.Slug,
                candidate.incident.CreatedAt,
                candidate.incident.UpdatedAt,
                candidate.incident.ResolvedAt
            });

        if (latestFirst)
        {
            query = query.OrderByDescending(incident => incident.CreatedAt);
        }

        return query.Select(incident => new IncidentResponse(
            incident.Id,
            incident.Title,
            incident.Status.ToString(),
            incident.Summary,
            incident.RootCauseSummary,
            incident.PostmortemDraft,
            incident.ProjectId,
            incident.ProjectName,
            incident.ProjectSlug,
            incident.EnvironmentId,
            incident.EnvironmentName,
            incident.EnvironmentSlug,
            incident.CreatedAt,
            incident.UpdatedAt,
            incident.ResolvedAt));
    }

    private static IQueryable<IncidentUpdateResponse> QueryIncidentUpdateResponses(
        DevControlDbContext dbContext,
        Guid organizationId,
        Guid incidentId,
        bool publicOnly,
        bool latestFirst = false,
        bool oldestFirst = false)
    {
        var updates = dbContext.IncidentUpdates
            .Where(update => update.OrganizationId == organizationId && update.IncidentId == incidentId);
        if (publicOnly)
        {
            updates = updates.Where(update => update.Visibility == IncidentUpdateVisibility.Public);
        }

        var query = updates.Select(update => new
        {
            update.Id,
            update.IncidentId,
            update.Status,
            update.Visibility,
            update.Message,
            update.CreatedByEmail,
            update.CreatedAt
        });

        if (latestFirst)
        {
            query = query.OrderByDescending(update => update.CreatedAt);
        }
        else if (oldestFirst)
        {
            query = query.OrderBy(update => update.CreatedAt);
        }

        return query.Select(update => new IncidentUpdateResponse(
            update.Id,
            update.IncidentId,
            update.Status.ToString(),
            update.Visibility.ToString(),
            update.Message,
            update.CreatedByEmail,
            update.CreatedAt));
    }

    private static IQueryable<ReleaseResponse> QueryReleaseResponses(
        DevControlDbContext dbContext,
        Guid organizationId,
        Guid? releaseId = null,
        bool latestFirst = false)
    {
        var releases = dbContext.StatusReleases
            .Where(release => release.OrganizationId == organizationId);
        if (releaseId is { } id)
        {
            releases = releases.Where(release => release.Id == id);
        }

        var query = releases
            .Join(dbContext.Projects, release => release.ProjectId, project => project.Id, (release, project) => new { release, project })
            .Join(dbContext.ProjectEnvironments, candidate => candidate.release.EnvironmentId, environment => environment.Id, (candidate, environment) => new
            {
                candidate.release.Id,
                candidate.release.Title,
                candidate.release.Version,
                candidate.release.Body,
                candidate.release.Status,
                candidate.release.ProjectId,
                ProjectName = candidate.project.Name,
                ProjectSlug = candidate.project.Slug,
                candidate.release.EnvironmentId,
                EnvironmentName = environment.Name,
                EnvironmentSlug = environment.Slug,
                candidate.release.CreatedAt,
                candidate.release.UpdatedAt,
                candidate.release.PublishedAt
            });

        if (latestFirst)
        {
            query = query.OrderByDescending(release => release.CreatedAt);
        }

        return query.Select(release => new ReleaseResponse(
            release.Id,
            release.Title,
            release.Version,
            release.Body,
            release.Status.ToString(),
            release.ProjectId,
            release.ProjectName,
            release.ProjectSlug,
            release.EnvironmentId,
            release.EnvironmentName,
            release.EnvironmentSlug,
            release.CreatedAt,
            release.UpdatedAt,
            release.PublishedAt));
    }

    private static async Task<ScopedEnvironment?> LoadScopedEnvironmentAsync(
        DevControlDbContext dbContext,
        Guid organizationId,
        Guid projectId,
        Guid environmentId,
        CancellationToken cancellationToken)
    {
        var project = await dbContext.Projects
            .SingleOrDefaultAsync(candidate => candidate.OrganizationId == organizationId && candidate.Id == projectId, cancellationToken);
        var environment = await dbContext.ProjectEnvironments
            .SingleOrDefaultAsync(
                candidate =>
                    candidate.OrganizationId == organizationId &&
                    candidate.ProjectId == projectId &&
                    candidate.Id == environmentId,
                cancellationToken);

        return project is null || environment is null ? null : new ScopedEnvironment(project, environment);
    }

    private static IncidentResponse ToIncidentResponse(Incident incident, Project project, ProjectEnvironment environment)
    {
        return new IncidentResponse(
            incident.Id,
            incident.Title,
            incident.Status.ToString(),
            incident.Summary,
            incident.RootCauseSummary,
            incident.PostmortemDraft,
            incident.ProjectId,
            project.Name,
            project.Slug,
            incident.EnvironmentId,
            environment.Name,
            environment.Slug,
            incident.CreatedAt,
            incident.UpdatedAt,
            incident.ResolvedAt);
    }

    private static ReleaseResponse ToReleaseResponse(StatusRelease release, Project project, ProjectEnvironment environment)
    {
        return new ReleaseResponse(
            release.Id,
            release.Title,
            release.Version,
            release.Body,
            release.Status.ToString(),
            release.ProjectId,
            project.Name,
            project.Slug,
            release.EnvironmentId,
            environment.Name,
            environment.Slug,
            release.CreatedAt,
            release.UpdatedAt,
            release.PublishedAt);
    }

    private static IncidentUpdateResponse ToIncidentUpdateResponse(IncidentUpdate update)
    {
        return new IncidentUpdateResponse(
            update.Id,
            update.IncidentId,
            update.Status.ToString(),
            update.Visibility.ToString(),
            update.Message,
            update.CreatedByEmail,
            update.CreatedAt);
    }

    private static async Task PublishIncidentEventAsync(
        WebhookEventPublisher webhookEventPublisher,
        string eventType,
        Incident incident,
        CurrentUser actor,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        await webhookEventPublisher.PublishAsync(
            incident.OrganizationId,
            incident.ProjectId,
            incident.EnvironmentId,
            eventType,
            "incident",
            incident.Id.ToString(),
            actor.Id,
            actor.Email,
            new
            {
                incident.Id,
                incident.Title,
                incident.Status,
                incident.Summary
            },
            now,
            cancellationToken);
    }

    private static void AddCompletedControlAction(
        DevControlDbContext dbContext,
        Guid organizationId,
        Guid? projectId,
        Guid? environmentId,
        CurrentUser actor,
        string actionType,
        string targetType,
        string? targetId,
        object request,
        object result,
        DateTimeOffset now)
    {
        var controlAction = new ControlAction(
            organizationId,
            projectId,
            environmentId,
            actionType,
            actor.Id,
            targetType,
            targetId,
            JsonSerializer.Serialize(request, JsonOptions),
            now);
        controlAction.MarkStarted(now);
        controlAction.MarkCompleted(ControlActionStatus.Succeeded, JsonSerializer.Serialize(result, JsonOptions), null, now);
        dbContext.ControlActions.Add(controlAction);
    }

    private static bool TryParseIncidentStatus(string value, out IncidentStatus status, out string? error)
    {
        if (Enum.TryParse(value, ignoreCase: true, out status) && Enum.IsDefined(status))
        {
            error = null;
            return true;
        }

        status = IncidentStatus.Investigating;
        error = "Incident status must be Investigating, Identified, Monitoring, or Resolved.";
        return false;
    }

    private static bool IsProduction(ProjectEnvironment environment)
    {
        return environment.Slug.Equals("production", StringComparison.OrdinalIgnoreCase);
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

    private sealed record ScopedEnvironment(Project Project, ProjectEnvironment Environment);

    private sealed record NormalizedMonitorRequest(
        string Name,
        string Url,
        int IntervalSeconds,
        int TimeoutSeconds,
        int SlowThresholdMilliseconds,
        int FailureThreshold,
        int RecoveryThreshold,
        IResult? Failure)
    {
        public static NormalizedMonitorRequest Valid(
            string name,
            string url,
            int intervalSeconds,
            int timeoutSeconds,
            int slowThresholdMilliseconds,
            int failureThreshold,
            int recoveryThreshold) => new(name, url, intervalSeconds, timeoutSeconds, slowThresholdMilliseconds, failureThreshold, recoveryThreshold, null);

        public static NormalizedMonitorRequest Failed(IResult failure) => new(string.Empty, string.Empty, 0, 0, 0, 0, 0, failure);
    }
}

public sealed record MonitorUpdateRequest(
    string? Name,
    string? Url,
    int? IntervalSeconds,
    int? TimeoutSeconds,
    int? SlowThresholdMilliseconds,
    int? FailureThreshold,
    int? RecoveryThreshold);

public sealed record MonitorResponse(
    Guid Id,
    Guid? LiveAppId,
    string Name,
    string Url,
    bool IsManagedFromLiveApp,
    bool IsPaused,
    string CurrentStatus,
    int IntervalSeconds,
    int TimeoutSeconds,
    int SlowThresholdMilliseconds,
    int FailureThreshold,
    int RecoveryThreshold,
    int ConsecutiveFailures,
    int ConsecutiveRecoveries,
    Guid ProjectId,
    string ProjectName,
    string ProjectSlug,
    Guid EnvironmentId,
    string EnvironmentName,
    string EnvironmentSlug,
    DateTimeOffset NextCheckAt,
    DateTimeOffset? LastCheckedAt,
    DateTimeOffset? LastSuccessAt,
    DateTimeOffset? LastFailureAt,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record MonitorCheckResponse(
    Guid Id,
    Guid MonitorId,
    string Status,
    bool Succeeded,
    int? StatusCode,
    string ResultKind,
    long DurationMilliseconds,
    string Error,
    string ResponsePreview,
    bool ResponseTruncated,
    DateTimeOffset CheckedAt);

public sealed record IncidentCreateRequest(string? Title, string? Summary, string? Message, bool Private);

public sealed record IncidentUpdateRequest(
    string? Title,
    string? Summary,
    string? Status,
    string? RootCauseSummary,
    string? PostmortemDraft,
    string? Message,
    bool Private);

public sealed record IncidentTimelineUpdateRequest(string? Message, string? Status, bool Private);

public sealed record IncidentResponse(
    Guid Id,
    string Title,
    string Status,
    string Summary,
    string RootCauseSummary,
    string PostmortemDraft,
    Guid ProjectId,
    string ProjectName,
    string ProjectSlug,
    Guid EnvironmentId,
    string EnvironmentName,
    string EnvironmentSlug,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? ResolvedAt);

public sealed record IncidentUpdateResponse(
    Guid Id,
    Guid IncidentId,
    string Status,
    string Visibility,
    string Message,
    string CreatedByEmail,
    DateTimeOffset CreatedAt);

public sealed record ReleaseCreateRequest(string? Title, string? Version, string? Body);

public sealed record ReleaseResponse(
    Guid Id,
    string Title,
    string Version,
    string Body,
    string Status,
    Guid ProjectId,
    string ProjectName,
    string ProjectSlug,
    Guid EnvironmentId,
    string EnvironmentName,
    string EnvironmentSlug,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? PublishedAt);

public sealed record PublicStatusPageResponse(
    string OrganizationName,
    string OrganizationSlug,
    string ProjectName,
    string ProjectSlug,
    string OverallStatus,
    IReadOnlyList<PublicEnvironmentResponse> Environments,
    IReadOnlyList<PublicMonitorStatusResponse> Monitors,
    IReadOnlyList<PublicIncidentResponse> Incidents,
    IReadOnlyList<PublicReleaseResponse> Releases);

public sealed record PublicEnvironmentResponse(string Name, string Slug);

public sealed record PublicMonitorStatusResponse(
    Guid Id,
    string Name,
    string EnvironmentName,
    string EnvironmentSlug,
    string Status,
    DateTimeOffset? LastCheckedAt,
    DateTimeOffset? LastSuccessAt,
    DateTimeOffset? LastFailureAt,
    int ChecksLast24Hours,
    double UptimePercentLast24Hours);

public sealed record PublicIncidentResponse(
    Guid Id,
    string Title,
    string Status,
    string Summary,
    string EnvironmentName,
    string EnvironmentSlug,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ResolvedAt,
    IReadOnlyList<IncidentUpdateResponse> Updates);

public sealed record PublicReleaseResponse(
    Guid Id,
    string Title,
    string Version,
    string Body,
    string EnvironmentName,
    string EnvironmentSlug,
    DateTimeOffset PublishedAt);
