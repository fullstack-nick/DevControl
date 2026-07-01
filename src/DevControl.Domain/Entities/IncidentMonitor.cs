namespace DevControl.Domain.Entities;

public sealed class IncidentMonitor
{
    public Guid Id { get; private set; }

    public Guid IncidentId { get; private set; }

    public Guid UptimeMonitorId { get; private set; }

    public Guid OrganizationId { get; private set; }

    public Guid ProjectId { get; private set; }

    public Guid EnvironmentId { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    private IncidentMonitor()
    {
    }

    public IncidentMonitor(Incident incident, UptimeMonitor monitor, DateTimeOffset now)
    {
        Id = Guid.NewGuid();
        IncidentId = incident.Id;
        UptimeMonitorId = monitor.Id;
        OrganizationId = incident.OrganizationId;
        ProjectId = incident.ProjectId;
        EnvironmentId = incident.EnvironmentId;
        CreatedAt = now;
    }
}
