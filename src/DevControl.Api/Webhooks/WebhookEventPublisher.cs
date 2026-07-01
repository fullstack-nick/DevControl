using System.Text.Json;
using DevControl.Application.Webhooks;
using DevControl.Domain.Entities;
using DevControl.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;

namespace DevControl.Api.Webhooks;

public sealed class WebhookEventPublisher(DevControlDbContext dbContext)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<WebhookEvent> PublishAsync(
        Guid organizationId,
        Guid projectId,
        Guid environmentId,
        string eventType,
        string resourceType,
        string? resourceId,
        Guid? actorUserId,
        string actorEmail,
        object data,
        DateTimeOffset occurredAt,
        CancellationToken cancellationToken)
    {
        var payloadJson = JsonSerializer.Serialize(new
        {
            id = Guid.NewGuid(),
            eventType,
            occurredAt,
            organizationId,
            projectId,
            environmentId,
            resource = new
            {
                type = resourceType,
                id = resourceId
            },
            actor = new
            {
                userId = actorUserId,
                email = string.IsNullOrWhiteSpace(actorEmail) ? "system" : actorEmail
            },
            data
        }, JsonOptions);

        var webhookEvent = new WebhookEvent(
            organizationId,
            projectId,
            environmentId,
            eventType,
            resourceType,
            resourceId,
            actorUserId,
            actorEmail,
            payloadJson,
            occurredAt);
        dbContext.WebhookEvents.Add(webhookEvent);

        var endpoints = await dbContext.WebhookEndpoints
            .Where(endpoint =>
                endpoint.OrganizationId == organizationId &&
                endpoint.ProjectId == projectId &&
                endpoint.EnvironmentId == environmentId)
            .ToListAsync(cancellationToken);

        foreach (var endpoint in endpoints)
        {
            if (!WebhookEventTypes.FromJson(endpoint.EventTypesJson).Contains(eventType, StringComparer.Ordinal))
            {
                continue;
            }

            var delivery = new WebhookDelivery(organizationId, projectId, environmentId, endpoint.Id, webhookEvent.Id, occurredAt);
            if (endpoint.IsPaused)
            {
                delivery.MarkSkippedPaused(occurredAt);
            }

            dbContext.WebhookDeliveries.Add(delivery);
        }

        return webhookEvent;
    }

    public WebhookDelivery PublishTestDelivery(
        WebhookEndpoint endpoint,
        Guid actorUserId,
        string actorEmail,
        DateTimeOffset occurredAt)
    {
        var payloadJson = JsonSerializer.Serialize(new
        {
            id = Guid.NewGuid(),
            eventType = WebhookEventTypes.Test,
            occurredAt,
            endpoint.OrganizationId,
            endpoint.ProjectId,
            endpoint.EnvironmentId,
            resource = new
            {
                type = "webhook_endpoint",
                id = endpoint.Id.ToString()
            },
            actor = new
            {
                userId = actorUserId,
                email = actorEmail
            },
            data = new
            {
                message = "DevControl webhook test delivery.",
                endpointId = endpoint.Id,
                endpointName = endpoint.Name
            }
        }, JsonOptions);

        var webhookEvent = new WebhookEvent(
            endpoint.OrganizationId,
            endpoint.ProjectId,
            endpoint.EnvironmentId,
            WebhookEventTypes.Test,
            "webhook_endpoint",
            endpoint.Id.ToString(),
            actorUserId,
            actorEmail,
            payloadJson,
            occurredAt);
        var delivery = new WebhookDelivery(
            endpoint.OrganizationId,
            endpoint.ProjectId,
            endpoint.EnvironmentId,
            endpoint.Id,
            webhookEvent.Id,
            occurredAt);
        if (endpoint.IsPaused)
        {
            delivery.MarkSkippedPaused(occurredAt);
        }

        dbContext.WebhookEvents.Add(webhookEvent);
        dbContext.WebhookDeliveries.Add(delivery);
        return delivery;
    }
}
