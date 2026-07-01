using DevControl.Domain.Entities;
using DevControl.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;

namespace DevControl.Api.Monitoring;

public sealed class MonitorProvisioningService(DevControlDbContext dbContext)
{
    public async Task<UptimeMonitor> EnsureManagedMonitorAsync(
        LiveApp liveApp,
        Guid? actorUserId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var monitor = await dbContext.UptimeMonitors
            .SingleOrDefaultAsync(candidate => candidate.LiveAppId == liveApp.Id, cancellationToken);

        var name = $"Health: {liveApp.Repo}";
        if (monitor is null)
        {
            monitor = new UptimeMonitor(
                liveApp.OrganizationId,
                liveApp.ProjectId,
                liveApp.EnvironmentId,
                liveApp.Id,
                name,
                liveApp.HealthUrl,
                isManagedFromLiveApp: true,
                actorUserId,
                now);
            dbContext.UptimeMonitors.Add(monitor);
        }
        else
        {
            monitor.UpdateDefinition(name, liveApp.HealthUrl, actorUserId, now);
        }

        return monitor;
    }
}
