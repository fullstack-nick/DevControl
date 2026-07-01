using DevControl.Domain.Enums;

namespace DevControl.Domain.Entities;

public sealed class WebhookDelivery
{
    public const int DefaultMaxAttempts = 5;

    public Guid Id { get; private set; }

    public Guid OrganizationId { get; private set; }

    public Guid ProjectId { get; private set; }

    public Guid EnvironmentId { get; private set; }

    public Guid WebhookEndpointId { get; private set; }

    public Guid WebhookEventId { get; private set; }

    public WebhookDeliveryStatus Status { get; private set; }

    public int AttemptCount { get; private set; }

    public int MaxAttempts { get; private set; } = DefaultMaxAttempts;

    public DateTimeOffset? NextAttemptAt { get; private set; }

    public DateTimeOffset? LastAttemptAt { get; private set; }

    public DateTimeOffset? CompletedAt { get; private set; }

    public int? LastStatusCode { get; private set; }

    public string LastError { get; private set; } = string.Empty;

    public string LastResponsePreview { get; private set; } = string.Empty;

    public bool LastResponseTruncated { get; private set; }

    public string? ProcessingLeaseId { get; private set; }

    public DateTimeOffset? ProcessingLeaseExpiresAt { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    private WebhookDelivery()
    {
    }

    public WebhookDelivery(
        Guid organizationId,
        Guid projectId,
        Guid environmentId,
        Guid webhookEndpointId,
        Guid webhookEventId,
        DateTimeOffset now,
        int maxAttempts = DefaultMaxAttempts)
    {
        if (maxAttempts <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxAttempts), "Max attempts must be positive.");
        }

        Id = Guid.NewGuid();
        OrganizationId = organizationId;
        ProjectId = projectId;
        EnvironmentId = environmentId;
        WebhookEndpointId = webhookEndpointId;
        WebhookEventId = webhookEventId;
        Status = WebhookDeliveryStatus.Pending;
        MaxAttempts = maxAttempts;
        NextAttemptAt = now;
        CreatedAt = now;
        UpdatedAt = now;
    }

    public void MarkSkippedPaused(DateTimeOffset now)
    {
        Status = WebhookDeliveryStatus.SkippedPaused;
        NextAttemptAt = null;
        CompletedAt = now;
        UpdatedAt = now;
    }

    public void Lease(string leaseId, DateTimeOffset leaseExpiresAt, DateTimeOffset now)
    {
        ProcessingLeaseId = string.IsNullOrWhiteSpace(leaseId) ? throw new ArgumentException("Lease id is required.", nameof(leaseId)) : leaseId.Trim();
        ProcessingLeaseExpiresAt = leaseExpiresAt;
        UpdatedAt = now;
    }

    public void ReleaseLease(DateTimeOffset now)
    {
        ProcessingLeaseId = null;
        ProcessingLeaseExpiresAt = null;
        UpdatedAt = now;
    }

    public void RecordAttempt(
        bool succeeded,
        bool retryable,
        int? statusCode,
        string? error,
        string? responsePreview,
        bool responseTruncated,
        DateTimeOffset? nextAttemptAt,
        DateTimeOffset now)
    {
        AttemptCount++;
        LastAttemptAt = now;
        LastStatusCode = statusCode;
        LastError = string.IsNullOrWhiteSpace(error) ? string.Empty : error.Trim();
        LastResponsePreview = string.IsNullOrWhiteSpace(responsePreview) ? string.Empty : responsePreview.Trim();
        LastResponseTruncated = responseTruncated;
        ProcessingLeaseId = null;
        ProcessingLeaseExpiresAt = null;

        if (succeeded)
        {
            Status = WebhookDeliveryStatus.Succeeded;
            NextAttemptAt = null;
            CompletedAt = now;
        }
        else if (retryable && AttemptCount < MaxAttempts && nextAttemptAt is not null)
        {
            Status = WebhookDeliveryStatus.Failed;
            NextAttemptAt = nextAttemptAt;
            CompletedAt = null;
        }
        else
        {
            Status = WebhookDeliveryStatus.Exhausted;
            NextAttemptAt = null;
            CompletedAt = now;
        }

        UpdatedAt = now;
    }

    public bool CanRetry(DateTimeOffset now)
    {
        return Status is WebhookDeliveryStatus.Pending or WebhookDeliveryStatus.Failed &&
            AttemptCount < MaxAttempts &&
            NextAttemptAt <= now &&
            (ProcessingLeaseExpiresAt is null || ProcessingLeaseExpiresAt <= now);
    }

    public void ScheduleImmediateRetry(DateTimeOffset now)
    {
        if (Status == WebhookDeliveryStatus.Succeeded)
        {
            return;
        }

        Status = WebhookDeliveryStatus.Pending;
        NextAttemptAt = now;
        CompletedAt = null;
        ProcessingLeaseId = null;
        ProcessingLeaseExpiresAt = null;
        UpdatedAt = now;
    }
}
