using DevControl.Domain.Enums;

namespace DevControl.Domain.Entities;

public sealed class Incident
{
    public Guid Id { get; private set; }

    public Guid OrganizationId { get; private set; }

    public Guid ProjectId { get; private set; }

    public Guid EnvironmentId { get; private set; }

    public string Title { get; private set; } = string.Empty;

    public IncidentStatus Status { get; private set; }

    public string Summary { get; private set; } = string.Empty;

    public string RootCauseSummary { get; private set; } = string.Empty;

    public string PostmortemDraft { get; private set; } = string.Empty;

    public Guid? CreatedByUserId { get; private set; }

    public Guid? UpdatedByUserId { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public DateTimeOffset? ResolvedAt { get; private set; }

    private Incident()
    {
    }

    public Incident(
        Guid organizationId,
        Guid projectId,
        Guid environmentId,
        string title,
        string summary,
        Guid? createdByUserId,
        DateTimeOffset now)
    {
        Id = Guid.NewGuid();
        OrganizationId = organizationId;
        ProjectId = projectId;
        EnvironmentId = environmentId;
        Title = Require(title, nameof(title), 200);
        Summary = Optional(summary, 2000);
        Status = IncidentStatus.Investigating;
        CreatedByUserId = createdByUserId;
        UpdatedByUserId = createdByUserId;
        CreatedAt = now;
        UpdatedAt = now;
    }

    public void Update(
        string title,
        string summary,
        IncidentStatus status,
        string rootCauseSummary,
        string postmortemDraft,
        Guid? updatedByUserId,
        DateTimeOffset now)
    {
        Title = Require(title, nameof(title), 200);
        Summary = Optional(summary, 2000);
        RootCauseSummary = Optional(rootCauseSummary, 4000);
        PostmortemDraft = Optional(postmortemDraft, 8000);
        Status = status;
        UpdatedByUserId = updatedByUserId;
        UpdatedAt = now;
        ResolvedAt = status == IncidentStatus.Resolved ? ResolvedAt ?? now : null;
    }

    public void Resolve(Guid? updatedByUserId, DateTimeOffset now)
    {
        Status = IncidentStatus.Resolved;
        UpdatedByUserId = updatedByUserId;
        UpdatedAt = now;
        ResolvedAt ??= now;
    }

    private static string Require(string value, string paramName, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException($"{paramName} is required.", paramName);
        }

        return Optional(value, maxLength);
    }

    private static string Optional(string? value, int maxLength)
    {
        var trimmed = value?.Trim() ?? string.Empty;
        if (trimmed.Length > maxLength)
        {
            throw new ArgumentException($"Value cannot exceed {maxLength} characters.");
        }

        return trimmed;
    }
}
