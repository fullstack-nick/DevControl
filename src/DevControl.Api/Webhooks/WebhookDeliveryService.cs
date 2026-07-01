using System.Net;
using System.Text.Json;
using DevControl.Application.Outbound;
using DevControl.Application.Webhooks;
using DevControl.Domain.Entities;
using DevControl.Domain.Enums;
using DevControl.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;

namespace DevControl.Api.Webhooks;

public sealed class WebhookDeliveryService(
    DevControlDbContext dbContext,
    WebhookSecretService secretService,
    ISafeOutboundHttpClient outboundHttpClient,
    TimeProvider timeProvider)
{
    private static readonly TimeSpan[] Backoff =
    [
        TimeSpan.FromMinutes(1),
        TimeSpan.FromMinutes(5),
        TimeSpan.FromMinutes(15),
        TimeSpan.FromHours(1),
        TimeSpan.FromHours(6)
    ];

    public async Task<WebhookDelivery?> DeliverAsync(Guid deliveryId, CancellationToken cancellationToken)
    {
        var delivery = await dbContext.WebhookDeliveries
            .SingleOrDefaultAsync(candidate => candidate.Id == deliveryId, cancellationToken);
        if (delivery is null)
        {
            return null;
        }

        var endpoint = await dbContext.WebhookEndpoints
            .SingleAsync(candidate => candidate.Id == delivery.WebhookEndpointId, cancellationToken);
        var webhookEvent = await dbContext.WebhookEvents
            .SingleAsync(candidate => candidate.Id == delivery.WebhookEventId, cancellationToken);
        var now = timeProvider.GetUtcNow();

        if (endpoint.IsPaused)
        {
            delivery.MarkSkippedPaused(now);
            await dbContext.SaveChangesAsync(cancellationToken);
            return delivery;
        }

        var secret = secretService.Unprotect(endpoint.ProtectedSecret);
        var signature = WebhookSignature.Sign(secret, now, delivery.Id, webhookEvent.PayloadJson);
        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["User-Agent"] = "DevControl-Webhooks/1.0",
            ["X-DevControl-Webhook-Id"] = endpoint.Id.ToString(),
            ["X-DevControl-Event"] = webhookEvent.EventType,
            ["X-DevControl-Delivery"] = delivery.Id.ToString(),
            ["X-DevControl-Timestamp"] = now.ToUnixTimeSeconds().ToString(),
            ["X-DevControl-Signature"] = signature
        };

        var response = await outboundHttpClient.SendAsync(
            new SafeOutboundRequest(
                new Uri(endpoint.Url),
                HttpMethod.Post,
                headers,
                webhookEvent.PayloadJson,
                "application/json",
                OutboundRequestPolicy.Webhook),
            cancellationToken);

        now = timeProvider.GetUtcNow();
        var retryable = IsRetryable(response);
        var nextAttemptAt = retryable ? now.Add(GetBackoff(delivery.AttemptCount)) : (DateTimeOffset?)null;
        var statusCode = response.StatusCode is null ? (int?)null : (int)response.StatusCode.Value;
        var error = response.Error ?? (response.IsSuccess ? null : $"Webhook target returned HTTP {statusCode}.");

        delivery.RecordAttempt(
            response.IsSuccess,
            retryable,
            statusCode,
            error,
            response.ResponsePreview,
            response.ResponseTruncated,
            nextAttemptAt,
            now);
        endpoint.RecordDeliveryResult(response.IsSuccess, now);
        dbContext.WebhookDeliveryAttempts.Add(new WebhookDeliveryAttempt(
            delivery.OrganizationId,
            delivery.ProjectId,
            delivery.EnvironmentId,
            delivery.WebhookEndpointId,
            delivery.WebhookEventId,
            delivery.Id,
            delivery.AttemptCount,
            response.Kind.ToString(),
            response.IsSuccess,
            statusCode,
            (long)Math.Round(response.Duration.TotalMilliseconds),
            error,
            response.ResponsePreview,
            response.ResponseTruncated,
            response.ResponseBytesRead,
            now));

        await dbContext.SaveChangesAsync(cancellationToken);
        return delivery;
    }

    public async Task<WebhookRetryBatchResult> ProcessDueRetriesAsync(int batchSize, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var leaseId = Guid.NewGuid().ToString("N");
        var leaseExpiresAt = now.AddMinutes(2);
        var due = await dbContext.WebhookDeliveries
            .Where(delivery =>
                (delivery.Status == WebhookDeliveryStatus.Pending || delivery.Status == WebhookDeliveryStatus.Failed) &&
                delivery.AttemptCount < delivery.MaxAttempts &&
                delivery.NextAttemptAt <= now &&
                (delivery.ProcessingLeaseExpiresAt == null || delivery.ProcessingLeaseExpiresAt <= now))
            .OrderBy(delivery => delivery.NextAttemptAt)
            .Take(batchSize)
            .ToListAsync(cancellationToken);

        foreach (var delivery in due)
        {
            delivery.Lease(leaseId, leaseExpiresAt, now);
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        var processed = 0;
        foreach (var delivery in due)
        {
            await DeliverAsync(delivery.Id, cancellationToken);
            processed++;
        }

        return new WebhookRetryBatchResult(processed, batchSize);
    }

    private static bool IsRetryable(SafeOutboundResponse response)
    {
        if (response.IsSuccess)
        {
            return false;
        }

        if (response.Kind is SafeOutboundResultKind.Timeout or SafeOutboundResultKind.NetworkError)
        {
            return true;
        }

        return response.StatusCode is HttpStatusCode.RequestTimeout or
            (HttpStatusCode)429 or
            >= HttpStatusCode.InternalServerError;
    }

    private static TimeSpan GetBackoff(int previousAttemptCount)
    {
        return Backoff[Math.Min(previousAttemptCount, Backoff.Length - 1)];
    }
}

public sealed record WebhookRetryBatchResult(int Processed, int BatchSize);
