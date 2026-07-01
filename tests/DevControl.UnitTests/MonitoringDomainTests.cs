using DevControl.Domain.Entities;
using DevControl.Domain.Enums;
using Xunit;

namespace DevControl.UnitTests;

public sealed class MonitoringDomainTests
{
    [Fact]
    public void Monitor_RecordCheck_TracksFailureAndRecoveryCounters()
    {
        var now = DateTimeOffset.UtcNow;
        var monitor = new UptimeMonitor(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Sample health",
            "https://sample.example.com/health",
            isManagedFromLiveApp: true,
            Guid.NewGuid(),
            now);

        monitor.RecordCheck(MonitorStatus.Down, now.AddMinutes(1));

        Assert.Equal(MonitorStatus.Down, monitor.CurrentStatus);
        Assert.Equal(1, monitor.ConsecutiveFailures);
        Assert.Equal(0, monitor.ConsecutiveRecoveries);

        monitor.RecordCheck(MonitorStatus.Up, now.AddMinutes(2));

        Assert.Equal(MonitorStatus.Up, monitor.CurrentStatus);
        Assert.Equal(0, monitor.ConsecutiveFailures);
        Assert.Equal(1, monitor.ConsecutiveRecoveries);
    }

    [Fact]
    public void Incident_Update_ResolvesAndReopensLifecycleStates()
    {
        var now = DateTimeOffset.UtcNow;
        var incident = new Incident(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Sample outage",
            "Health checks are failing.",
            Guid.NewGuid(),
            now);

        incident.Update("Sample outage", "Health checks are failing.", IncidentStatus.Resolved, "Bad deploy.", "Rollback completed.", Guid.NewGuid(), now.AddMinutes(5));

        Assert.Equal(IncidentStatus.Resolved, incident.Status);
        Assert.NotNull(incident.ResolvedAt);
        Assert.Equal("Bad deploy.", incident.RootCauseSummary);
    }
}
