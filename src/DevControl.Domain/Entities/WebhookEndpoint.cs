namespace DevControl.Domain.Entities;

public sealed class WebhookEndpoint
{
    public Guid Id { get; private set; }

    public Guid OrganizationId { get; private set; }

    public Guid ProjectId { get; private set; }

    public Guid EnvironmentId { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public string Url { get; private set; } = string.Empty;

    public string SecretPrefix { get; private set; } = string.Empty;

    public string ProtectedSecret { get; private set; } = string.Empty;

    public string EventTypesJson { get; private set; } = "[]";

    public bool IsPaused { get; private set; }

    public Guid CreatedByUserId { get; private set; }

    public Guid? PausedByUserId { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public DateTimeOffset? PausedAt { get; private set; }

    public DateTimeOffset? LastDeliveryAt { get; private set; }

    public DateTimeOffset? LastSuccessAt { get; private set; }

    public DateTimeOffset? LastFailureAt { get; private set; }

    private WebhookEndpoint()
    {
    }

    public WebhookEndpoint(
        Guid organizationId,
        Guid projectId,
        Guid environmentId,
        string name,
        string url,
        string secretPrefix,
        string protectedSecret,
        string eventTypesJson,
        Guid createdByUserId,
        DateTimeOffset now)
    {
        Id = Guid.NewGuid();
        OrganizationId = organizationId;
        ProjectId = projectId;
        EnvironmentId = environmentId;
        Name = Require(name, nameof(name));
        Url = Require(url, nameof(url));
        SecretPrefix = Require(secretPrefix, nameof(secretPrefix));
        ProtectedSecret = Require(protectedSecret, nameof(protectedSecret));
        EventTypesJson = string.IsNullOrWhiteSpace(eventTypesJson) ? "[]" : eventTypesJson.Trim();
        CreatedByUserId = createdByUserId;
        CreatedAt = now;
        UpdatedAt = now;
    }

    public void Pause(Guid pausedByUserId, DateTimeOffset now)
    {
        if (IsPaused)
        {
            return;
        }

        IsPaused = true;
        PausedByUserId = pausedByUserId;
        PausedAt = now;
        UpdatedAt = now;
    }

    public void Resume(DateTimeOffset now)
    {
        if (!IsPaused)
        {
            return;
        }

        IsPaused = false;
        PausedByUserId = null;
        PausedAt = null;
        UpdatedAt = now;
    }

    public void RecordDeliveryResult(bool succeeded, DateTimeOffset now)
    {
        LastDeliveryAt = now;
        if (succeeded)
        {
            LastSuccessAt = now;
        }
        else
        {
            LastFailureAt = now;
        }

        UpdatedAt = now;
    }

    private static string Require(string value, string paramName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException($"{paramName} is required.", paramName);
        }

        return value.Trim();
    }
}
