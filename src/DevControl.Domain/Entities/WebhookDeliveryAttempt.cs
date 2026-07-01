namespace DevControl.Domain.Entities;

public sealed class WebhookDeliveryAttempt
{
    public Guid Id { get; private set; }

    public Guid OrganizationId { get; private set; }

    public Guid ProjectId { get; private set; }

    public Guid EnvironmentId { get; private set; }

    public Guid WebhookEndpointId { get; private set; }

    public Guid WebhookEventId { get; private set; }

    public Guid WebhookDeliveryId { get; private set; }

    public int AttemptNumber { get; private set; }

    public string ResultKind { get; private set; } = string.Empty;

    public bool Succeeded { get; private set; }

    public int? StatusCode { get; private set; }

    public long DurationMilliseconds { get; private set; }

    public string Error { get; private set; } = string.Empty;

    public string ResponsePreview { get; private set; } = string.Empty;

    public bool ResponseTruncated { get; private set; }

    public long ResponseBytesRead { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    private WebhookDeliveryAttempt()
    {
    }

    public WebhookDeliveryAttempt(
        Guid organizationId,
        Guid projectId,
        Guid environmentId,
        Guid webhookEndpointId,
        Guid webhookEventId,
        Guid webhookDeliveryId,
        int attemptNumber,
        string resultKind,
        bool succeeded,
        int? statusCode,
        long durationMilliseconds,
        string? error,
        string? responsePreview,
        bool responseTruncated,
        long responseBytesRead,
        DateTimeOffset now)
    {
        Id = Guid.NewGuid();
        OrganizationId = organizationId;
        ProjectId = projectId;
        EnvironmentId = environmentId;
        WebhookEndpointId = webhookEndpointId;
        WebhookEventId = webhookEventId;
        WebhookDeliveryId = webhookDeliveryId;
        AttemptNumber = attemptNumber;
        ResultKind = string.IsNullOrWhiteSpace(resultKind) ? "Unknown" : resultKind.Trim();
        Succeeded = succeeded;
        StatusCode = statusCode;
        DurationMilliseconds = Math.Max(0, durationMilliseconds);
        Error = string.IsNullOrWhiteSpace(error) ? string.Empty : error.Trim();
        ResponsePreview = string.IsNullOrWhiteSpace(responsePreview) ? string.Empty : responsePreview.Trim();
        ResponseTruncated = responseTruncated;
        ResponseBytesRead = Math.Max(0, responseBytesRead);
        CreatedAt = now;
    }
}
