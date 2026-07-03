using DevControl.Api.Webhooks;
using DevControl.Application.Webhooks;
using DevControl.Domain.Entities;
using DevControl.Domain.Enums;
using DevControl.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;

namespace DevControl.Api.Monitoring;

public sealed class IncidentAutomationService(
    DevControlDbContext dbContext,
    WebhookEventPublisher webhookEventPublisher)
{
    public async Task HandleMonitorResultAsync(
        UptimeMonitor monitor,
        MonitorCheck check,
        MonitorStatus previousStatus,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        if (check.Status == MonitorStatus.Down && monitor.ConsecutiveFailures >= monitor.FailureThreshold)
        {
            if (previousStatus != MonitorStatus.Down)
            {
                await PublishMonitorEventAsync(WebhookEventTypes.MonitorDown, monitor, check, now, cancellationToken);
            }

            if (!await HasActiveIncidentAsync(monitor.Id, cancellationToken))
            {
                await CreateIncidentAsync(monitor, check, now, cancellationToken);
            }

            return;
        }

        if (check.Status is MonitorStatus.Up or MonitorStatus.Slow &&
            monitor.ConsecutiveRecoveries >= monitor.RecoveryThreshold)
        {
            var hasActiveIncident = await HasActiveIncidentAsync(monitor.Id, cancellationToken);
            if (previousStatus == MonitorStatus.Down || hasActiveIncident)
            {
                await PublishMonitorEventAsync(WebhookEventTypes.MonitorRecovered, monitor, check, now, cancellationToken);
            }

            if (hasActiveIncident)
            {
                await ResolveActiveIncidentsAsync(monitor, check, now, cancellationToken);
            }
        }
    }

    private async Task<bool> HasActiveIncidentAsync(Guid monitorId, CancellationToken cancellationToken)
    {
        return await dbContext.IncidentMonitors
            .Where(link => link.UptimeMonitorId == monitorId)
            .Join(
                dbContext.Incidents,
                link => link.IncidentId,
                incident => incident.Id,
                (link, incident) => incident)
            .AnyAsync(incident => incident.Status != IncidentStatus.Resolved, cancellationToken);
    }

    private async Task CreateIncidentAsync(
        UptimeMonitor monitor,
        MonitorCheck check,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var title = $"Monitor down: {monitor.Name}";
        var summary = check.Error.Length == 0
            ? $"Health check failed for {monitor.Url}."
            : check.Error;
        var incident = new Incident(
            monitor.OrganizationId,
            monitor.ProjectId,
            monitor.EnvironmentId,
            title,
            summary,
            createdByUserId: null,
            now);
        var update = new IncidentUpdate(
            incident,
            IncidentStatus.Investigating,
            IncidentUpdateVisibility.Public,
            $"DevControl detected a failing health check for {monitor.Name}.",
            createdByUserId: null,
            createdByEmail: "system",
            now);

        dbContext.Incidents.Add(incident);
        dbContext.IncidentUpdates.Add(update);
        dbContext.IncidentMonitors.Add(new IncidentMonitor(incident, monitor, now));

        await webhookEventPublisher.PublishAsync(
            monitor.OrganizationId,
            monitor.ProjectId,
            monitor.EnvironmentId,
            WebhookEventTypes.IncidentCreated,
            "incident",
            incident.Id.ToString(),
            null,
            "system",
            new
            {
                incident.Id,
                incident.Title,
                incident.Status,
                monitorId = monitor.Id,
                monitor.Name,
                monitor.Url,
                check.StatusCode,
                check.Error
            },
            now,
            cancellationToken);
    }

    private async Task ResolveActiveIncidentsAsync(
        UptimeMonitor monitor,
        MonitorCheck check,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var active = await dbContext.IncidentMonitors
            .Where(link => link.UptimeMonitorId == monitor.Id)
            .Join(
                dbContext.Incidents,
                link => link.IncidentId,
                incident => incident.Id,
                (link, incident) => incident)
            .Where(incident => incident.Status != IncidentStatus.Resolved)
            .ToListAsync(cancellationToken);

        foreach (var incident in active)
        {
            incident.Resolve(null, now);
            dbContext.IncidentUpdates.Add(new IncidentUpdate(
                incident,
                IncidentStatus.Resolved,
                IncidentUpdateVisibility.Public,
                $"DevControl detected recovery for {monitor.Name}.",
                createdByUserId: null,
                createdByEmail: "system",
                now));

            await webhookEventPublisher.PublishAsync(
                monitor.OrganizationId,
                monitor.ProjectId,
                monitor.EnvironmentId,
                WebhookEventTypes.IncidentResolved,
                "incident",
                incident.Id.ToString(),
                null,
                "system",
                new
                {
                    incident.Id,
                    incident.Title,
                    incident.Status,
                    monitorId = monitor.Id,
                    monitor.Name,
                    check.StatusCode
                },
                now,
                cancellationToken);
        }
    }

    private async Task PublishMonitorEventAsync(
        string eventType,
        UptimeMonitor monitor,
        MonitorCheck check,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        await webhookEventPublisher.PublishAsync(
            monitor.OrganizationId,
            monitor.ProjectId,
            monitor.EnvironmentId,
            eventType,
            "uptime_monitor",
            monitor.Id.ToString(),
            null,
            "system",
            new
            {
                monitor.Id,
                monitor.Name,
                monitor.Url,
                status = check.Status,
                check.StatusCode,
                check.DurationMilliseconds,
                check.Error
            },
            now,
            cancellationToken);
    }
}
