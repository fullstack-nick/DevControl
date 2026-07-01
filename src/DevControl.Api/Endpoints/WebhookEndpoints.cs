using System.Text.Json;
using DevControl.Api.Security;
using DevControl.Api.Webhooks;
using DevControl.Application.Security;
using DevControl.Application.Webhooks;
using DevControl.Domain.Entities;
using DevControl.Domain.Enums;
using DevControl.Infrastructure.Database;
using DevControl.Infrastructure.Outbound;
using Microsoft.EntityFrameworkCore;

namespace DevControl.Api.Endpoints;

public static class WebhookEndpoints
{
    private const int RetryBatchSize = 25;
    private const string SchedulerSecretHeader = "X-DevControl-Scheduler-Secret";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static void MapWebhookEndpoints(this WebApplication app)
    {
        app.MapPost("/internal/scheduler/tick", RunSchedulerTickAsync);

        var api = app.MapGroup("/api").RequireAuthorization();

        api.MapGet("/organizations/{organizationId:guid}/webhook-endpoints", ListWebhookEndpointsAsync);
        api.MapPost(
            "/organizations/{organizationId:guid}/projects/{projectId:guid}/environments/{environmentId:guid}/webhook-endpoints",
            CreateWebhookEndpointAsync).RequireCsrf();
        api.MapPost(
            "/organizations/{organizationId:guid}/webhook-endpoints/{endpointId:guid}/pause",
            PauseWebhookEndpointAsync).RequireCsrf();
        api.MapPost(
            "/organizations/{organizationId:guid}/webhook-endpoints/{endpointId:guid}/resume",
            ResumeWebhookEndpointAsync).RequireCsrf();
        api.MapPost(
            "/organizations/{organizationId:guid}/webhook-endpoints/{endpointId:guid}/test-deliveries",
            CreateTestDeliveryAsync).RequireCsrf();
        api.MapGet(
            "/organizations/{organizationId:guid}/webhook-endpoints/{endpointId:guid}/deliveries",
            ListWebhookDeliveriesAsync);
        api.MapPost(
            "/organizations/{organizationId:guid}/webhook-deliveries/{deliveryId:guid}/retry",
            RetryWebhookDeliveryAsync).RequireCsrf();
    }

    private static async Task<IResult> ListWebhookEndpointsAsync(
        Guid organizationId,
        CurrentUserAccessor currentUserAccessor,
        TenantAccessService tenantAccess,
        DevControlDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var actor = await currentUserAccessor.GetOrCreateAsync(cancellationToken);
        var access = await tenantAccess.RequireAsync(organizationId, actor, OrganizationRole.Viewer, cancellationToken);
        var failure = AccessFailure(access);
        if (failure is not null)
        {
            return failure;
        }

        var endpoints = await dbContext.WebhookEndpoints
            .Where(endpoint => endpoint.OrganizationId == organizationId)
            .Join(
                dbContext.Projects,
                endpoint => endpoint.ProjectId,
                project => project.Id,
                (endpoint, project) => new { endpoint, project })
            .Join(
                dbContext.ProjectEnvironments,
                candidate => candidate.endpoint.EnvironmentId,
                environment => environment.Id,
                (candidate, environment) => new { candidate.endpoint, candidate.project, environment })
            .OrderByDescending(candidate => candidate.endpoint.CreatedAt)
            .ToListAsync(cancellationToken);

        return Results.Ok(endpoints.Select(candidate => ToEndpointResponse(
            candidate.endpoint,
            candidate.project.Name,
            candidate.project.Slug,
            candidate.environment.Name,
            candidate.environment.Slug)));
    }

    private static async Task<IResult> CreateWebhookEndpointAsync(
        Guid organizationId,
        Guid projectId,
        Guid environmentId,
        WebhookEndpointCreateRequest request,
        CurrentUserAccessor currentUserAccessor,
        TenantAccessService tenantAccess,
        DevControlDbContext dbContext,
        AuditLogWriter auditLogWriter,
        WebhookSecretService secretService,
        OutboundRequestGuard outboundRequestGuard,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var actor = await currentUserAccessor.GetOrCreateAsync(cancellationToken);
        var access = await tenantAccess.RequireAsync(
            organizationId,
            actor,
            OrganizationRole.Admin,
            cancellationToken,
            auditDenied: true,
            deniedAction: "webhook_endpoint.create.denied",
            targetType: "webhook_endpoint");
        var failure = AccessFailure(access);
        if (failure is not null)
        {
            return failure;
        }

        var scope = await LoadScopedEnvironmentAsync(dbContext, organizationId, projectId, environmentId, cancellationToken);
        if (scope is null)
        {
            return Results.NotFound();
        }

        var validation = await ValidateCreateRequestAsync(request, outboundRequestGuard, cancellationToken);
        if (validation.Failure is not null)
        {
            return validation.Failure;
        }

        var secret = secretService.CreateSecret();
        var now = timeProvider.GetUtcNow();
        var endpoint = new WebhookEndpoint(
            organizationId,
            projectId,
            environmentId,
            validation.Name,
            validation.Url,
            secret.Prefix,
            secret.ProtectedSecret,
            WebhookEventTypes.ToJson(validation.EventTypes),
            actor.Id,
            now);

        dbContext.WebhookEndpoints.Add(endpoint);
        AddCompletedControlAction(
            dbContext,
            organizationId,
            projectId,
            environmentId,
            actor,
            "webhook_endpoint.create",
            "webhook_endpoint",
            endpoint.Id.ToString(),
            new { endpoint.Name, endpoint.Url, eventTypes = validation.EventTypes },
            new { endpoint.Id, endpoint.SecretPrefix },
            now);
        auditLogWriter.Add(
            organizationId,
            actor,
            "webhook_endpoint.create",
            "Succeeded",
            "webhook_endpoint",
            endpoint.Id.ToString(),
            "Webhook endpoint created.",
            new { endpoint.Name, endpoint.Url, endpoint.SecretPrefix, eventTypes = validation.EventTypes },
            projectId,
            environmentId);

        await dbContext.SaveChangesAsync(cancellationToken);
        var response = ToEndpointCreateResponse(endpoint, scope.Project.Name, scope.Project.Slug, scope.Environment.Name, scope.Environment.Slug, secret.Secret);
        return Results.Created($"/api/organizations/{organizationId}/webhook-endpoints/{endpoint.Id}", response);
    }

    private static async Task<IResult> PauseWebhookEndpointAsync(
        Guid organizationId,
        Guid endpointId,
        CurrentUserAccessor currentUserAccessor,
        TenantAccessService tenantAccess,
        DevControlDbContext dbContext,
        AuditLogWriter auditLogWriter,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        return await ChangePauseStateAsync(
            organizationId,
            endpointId,
            paused: true,
            currentUserAccessor,
            tenantAccess,
            dbContext,
            auditLogWriter,
            timeProvider,
            cancellationToken);
    }

    private static async Task<IResult> ResumeWebhookEndpointAsync(
        Guid organizationId,
        Guid endpointId,
        CurrentUserAccessor currentUserAccessor,
        TenantAccessService tenantAccess,
        DevControlDbContext dbContext,
        AuditLogWriter auditLogWriter,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        return await ChangePauseStateAsync(
            organizationId,
            endpointId,
            paused: false,
            currentUserAccessor,
            tenantAccess,
            dbContext,
            auditLogWriter,
            timeProvider,
            cancellationToken);
    }

    private static async Task<IResult> CreateTestDeliveryAsync(
        Guid organizationId,
        Guid endpointId,
        CurrentUserAccessor currentUserAccessor,
        TenantAccessService tenantAccess,
        DevControlDbContext dbContext,
        AuditLogWriter auditLogWriter,
        WebhookEventPublisher publisher,
        WebhookDeliveryService deliveryService,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var actor = await currentUserAccessor.GetOrCreateAsync(cancellationToken);
        var endpoint = await LoadEndpointAsync(dbContext, organizationId, endpointId, cancellationToken);
        if (endpoint is null)
        {
            return Results.NotFound();
        }

        var access = await RequireAdminAsync(
            organizationId,
            actor,
            tenantAccess,
            "webhook_endpoint.test.denied",
            endpoint.Id.ToString(),
            cancellationToken);
        if (access is not null)
        {
            return access;
        }

        var now = timeProvider.GetUtcNow();
        var delivery = publisher.PublishTestDelivery(endpoint, actor.Id, actor.Email, now);
        AddCompletedControlAction(
            dbContext,
            organizationId,
            endpoint.ProjectId,
            endpoint.EnvironmentId,
            actor,
            "webhook_endpoint.test",
            "webhook_delivery",
            delivery.Id.ToString(),
            new { endpoint.Id, endpoint.Url },
            new { delivery.Id, delivery.Status },
            now);
        auditLogWriter.Add(
            organizationId,
            actor,
            "webhook_endpoint.test",
            "Succeeded",
            "webhook_delivery",
            delivery.Id.ToString(),
            "Webhook test delivery requested.",
            new { endpoint.Id, endpoint.Name, endpoint.Url },
            endpoint.ProjectId,
            endpoint.EnvironmentId);

        await dbContext.SaveChangesAsync(cancellationToken);
        if (delivery.Status != WebhookDeliveryStatus.SkippedPaused)
        {
            await deliveryService.DeliverAsync(delivery.Id, cancellationToken);
        }

        var response = await LoadDeliveryResponseAsync(dbContext, organizationId, delivery.Id, cancellationToken);
        return Results.Ok(response);
    }

    private static async Task<IResult> ListWebhookDeliveriesAsync(
        Guid organizationId,
        Guid endpointId,
        CurrentUserAccessor currentUserAccessor,
        TenantAccessService tenantAccess,
        DevControlDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var actor = await currentUserAccessor.GetOrCreateAsync(cancellationToken);
        var access = await tenantAccess.RequireAsync(organizationId, actor, OrganizationRole.Viewer, cancellationToken);
        var failure = AccessFailure(access);
        if (failure is not null)
        {
            return failure;
        }

        if (!await dbContext.WebhookEndpoints.AnyAsync(endpoint => endpoint.OrganizationId == organizationId && endpoint.Id == endpointId, cancellationToken))
        {
            return Results.NotFound();
        }

        var deliveries = await QueryDeliveryResponses(dbContext, organizationId, endpointId, deliveryId: null)
            .OrderByDescending(delivery => delivery.CreatedAt)
            .Take(50)
            .ToListAsync(cancellationToken);

        return Results.Ok(deliveries);
    }

    private static async Task<IResult> RetryWebhookDeliveryAsync(
        Guid organizationId,
        Guid deliveryId,
        CurrentUserAccessor currentUserAccessor,
        TenantAccessService tenantAccess,
        DevControlDbContext dbContext,
        AuditLogWriter auditLogWriter,
        WebhookDeliveryService deliveryService,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var actor = await currentUserAccessor.GetOrCreateAsync(cancellationToken);
        var delivery = await dbContext.WebhookDeliveries
            .SingleOrDefaultAsync(candidate => candidate.OrganizationId == organizationId && candidate.Id == deliveryId, cancellationToken);
        if (delivery is null)
        {
            return Results.NotFound();
        }

        var access = await RequireAdminAsync(
            organizationId,
            actor,
            tenantAccess,
            "webhook_delivery.retry.denied",
            delivery.Id.ToString(),
            cancellationToken);
        if (access is not null)
        {
            return access;
        }

        var now = timeProvider.GetUtcNow();
        delivery.ScheduleImmediateRetry(now);
        AddCompletedControlAction(
            dbContext,
            organizationId,
            delivery.ProjectId,
            delivery.EnvironmentId,
            actor,
            "webhook_delivery.retry",
            "webhook_delivery",
            delivery.Id.ToString(),
            new { delivery.Id },
            new { scheduled = true },
            now);
        auditLogWriter.Add(
            organizationId,
            actor,
            "webhook_delivery.retry",
            "Succeeded",
            "webhook_delivery",
            delivery.Id.ToString(),
            "Webhook delivery retry requested.",
            new { delivery.Id, delivery.WebhookEndpointId, delivery.WebhookEventId },
            delivery.ProjectId,
            delivery.EnvironmentId);

        await dbContext.SaveChangesAsync(cancellationToken);
        await deliveryService.DeliverAsync(delivery.Id, cancellationToken);

        var response = await LoadDeliveryResponseAsync(dbContext, organizationId, delivery.Id, cancellationToken);
        return Results.Ok(response);
    }

    private static async Task<IResult> RunSchedulerTickAsync(
        HttpContext httpContext,
        IConfiguration configuration,
        WebhookDeliveryService deliveryService,
        CancellationToken cancellationToken)
    {
        var configuredSecret = configuration["SCHEDULER_SECRET"];
        if (string.IsNullOrWhiteSpace(configuredSecret))
        {
            return Results.NotFound();
        }

        var providedSecret = httpContext.Request.Headers[SchedulerSecretHeader].ToString();
        if (!OperatorSecretValidator.IsValid(configuredSecret, providedSecret))
        {
            return Results.Unauthorized();
        }

        var result = await deliveryService.ProcessDueRetriesAsync(RetryBatchSize, cancellationToken);
        return Results.Ok(result);
    }

    private static async Task<IResult> ChangePauseStateAsync(
        Guid organizationId,
        Guid endpointId,
        bool paused,
        CurrentUserAccessor currentUserAccessor,
        TenantAccessService tenantAccess,
        DevControlDbContext dbContext,
        AuditLogWriter auditLogWriter,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var actor = await currentUserAccessor.GetOrCreateAsync(cancellationToken);
        var endpoint = await LoadEndpointAsync(dbContext, organizationId, endpointId, cancellationToken);
        if (endpoint is null)
        {
            return Results.NotFound();
        }

        var access = await RequireAdminAsync(
            organizationId,
            actor,
            tenantAccess,
            paused ? "webhook_endpoint.pause.denied" : "webhook_endpoint.resume.denied",
            endpoint.Id.ToString(),
            cancellationToken);
        if (access is not null)
        {
            return access;
        }

        var now = timeProvider.GetUtcNow();
        if (paused)
        {
            endpoint.Pause(actor.Id, now);
        }
        else
        {
            endpoint.Resume(now);
        }

        var action = paused ? "webhook_endpoint.pause" : "webhook_endpoint.resume";
        AddCompletedControlAction(
            dbContext,
            organizationId,
            endpoint.ProjectId,
            endpoint.EnvironmentId,
            actor,
            action,
            "webhook_endpoint",
            endpoint.Id.ToString(),
            new { endpoint.Id },
            new { endpoint.IsPaused },
            now);
        auditLogWriter.Add(
            organizationId,
            actor,
            action,
            "Succeeded",
            "webhook_endpoint",
            endpoint.Id.ToString(),
            paused ? "Webhook endpoint paused." : "Webhook endpoint resumed.",
            new { endpoint.Id, endpoint.Name, endpoint.Url },
            endpoint.ProjectId,
            endpoint.EnvironmentId);

        await dbContext.SaveChangesAsync(cancellationToken);
        return Results.Ok(ToEndpointResponse(endpoint, string.Empty, string.Empty, string.Empty, string.Empty));
    }

    private static async Task<NormalizedWebhookCreateRequest> ValidateCreateRequestAsync(
        WebhookEndpointCreateRequest request,
        OutboundRequestGuard outboundRequestGuard,
        CancellationToken cancellationToken)
    {
        var name = request.Name?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(name))
        {
            return NormalizedWebhookCreateRequest.Failed(Results.BadRequest(new ProblemDetailsResponse("Webhook endpoint name is required.")));
        }

        if (name.Length > 160)
        {
            return NormalizedWebhookCreateRequest.Failed(Results.BadRequest(new ProblemDetailsResponse("Webhook endpoint name cannot exceed 160 characters.")));
        }

        var url = request.Url?.Trim() ?? string.Empty;
        if (url.Length > 1000)
        {
            return NormalizedWebhookCreateRequest.Failed(Results.BadRequest(new ProblemDetailsResponse("Webhook endpoint URL cannot exceed 1000 characters.")));
        }

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            return NormalizedWebhookCreateRequest.Failed(Results.BadRequest(new ProblemDetailsResponse("Webhook endpoint URL must be absolute.")));
        }

        var guardResult = await outboundRequestGuard.ValidateAsync(uri, Application.Outbound.OutboundRequestPolicy.Webhook, cancellationToken);
        if (!guardResult.IsAllowed)
        {
            return NormalizedWebhookCreateRequest.Failed(Results.BadRequest(new ProblemDetailsResponse(guardResult.Error ?? "Webhook endpoint URL is not allowed.")));
        }

        if (!WebhookEventTypes.TryNormalize(request.EventTypes, out var eventTypes, out var errors))
        {
            return NormalizedWebhookCreateRequest.Failed(Results.BadRequest(new ValidationProblemDetailsResponse(errors)));
        }

        return NormalizedWebhookCreateRequest.Valid(name, uri.ToString(), eventTypes);
    }

    private static async Task<ScopedEnvironment?> LoadScopedEnvironmentAsync(
        DevControlDbContext dbContext,
        Guid organizationId,
        Guid projectId,
        Guid environmentId,
        CancellationToken cancellationToken)
    {
        var project = await dbContext.Projects
            .SingleOrDefaultAsync(candidate => candidate.OrganizationId == organizationId && candidate.Id == projectId, cancellationToken);
        var environment = await dbContext.ProjectEnvironments
            .SingleOrDefaultAsync(
                candidate =>
                    candidate.OrganizationId == organizationId &&
                    candidate.ProjectId == projectId &&
                    candidate.Id == environmentId,
                cancellationToken);

        return project is null || environment is null ? null : new ScopedEnvironment(project, environment);
    }

    private static async Task<WebhookEndpoint?> LoadEndpointAsync(
        DevControlDbContext dbContext,
        Guid organizationId,
        Guid endpointId,
        CancellationToken cancellationToken)
    {
        return await dbContext.WebhookEndpoints
            .SingleOrDefaultAsync(candidate => candidate.OrganizationId == organizationId && candidate.Id == endpointId, cancellationToken);
    }

    private static async Task<IResult?> RequireAdminAsync(
        Guid organizationId,
        CurrentUser actor,
        TenantAccessService tenantAccess,
        string deniedAction,
        string? targetId,
        CancellationToken cancellationToken)
    {
        var access = await tenantAccess.RequireAsync(
            organizationId,
            actor,
            OrganizationRole.Admin,
            cancellationToken,
            auditDenied: true,
            deniedAction: deniedAction,
            targetType: "webhook_endpoint",
            targetId: targetId);
        return AccessFailure(access);
    }

    private static async Task<WebhookDeliveryResponse?> LoadDeliveryResponseAsync(
        DevControlDbContext dbContext,
        Guid organizationId,
        Guid deliveryId,
        CancellationToken cancellationToken)
    {
        return await QueryDeliveryResponses(dbContext, organizationId, endpointId: null, deliveryId)
            .SingleOrDefaultAsync(cancellationToken);
    }

    private static IQueryable<WebhookDeliveryResponse> QueryDeliveryResponses(
        DevControlDbContext dbContext,
        Guid organizationId,
        Guid? endpointId,
        Guid? deliveryId)
    {
        var deliveries = dbContext.WebhookDeliveries
            .Where(delivery => delivery.OrganizationId == organizationId);
        if (endpointId is not null)
        {
            deliveries = deliveries.Where(delivery => delivery.WebhookEndpointId == endpointId);
        }

        if (deliveryId is not null)
        {
            deliveries = deliveries.Where(delivery => delivery.Id == deliveryId);
        }

        return deliveries
            .Join(
                dbContext.WebhookEvents,
                delivery => delivery.WebhookEventId,
                webhookEvent => webhookEvent.Id,
                (delivery, webhookEvent) => new WebhookDeliveryResponse(
                    delivery.Id,
                    delivery.WebhookEndpointId,
                    delivery.WebhookEventId,
                    webhookEvent.EventType,
                    webhookEvent.ResourceType,
                    webhookEvent.ResourceId,
                    delivery.Status.ToString(),
                    delivery.AttemptCount,
                    delivery.MaxAttempts,
                    delivery.NextAttemptAt,
                    delivery.LastAttemptAt,
                    delivery.CompletedAt,
                    delivery.LastStatusCode,
                    delivery.LastError,
                    delivery.LastResponsePreview,
                    delivery.LastResponseTruncated,
                    delivery.CreatedAt));
    }

    private static void AddCompletedControlAction(
        DevControlDbContext dbContext,
        Guid organizationId,
        Guid? projectId,
        Guid? environmentId,
        CurrentUser actor,
        string actionType,
        string targetType,
        string? targetId,
        object request,
        object result,
        DateTimeOffset now)
    {
        var controlAction = new ControlAction(
            organizationId,
            projectId,
            environmentId,
            actionType,
            actor.Id,
            targetType,
            targetId,
            JsonSerializer.Serialize(request, JsonOptions),
            now);
        controlAction.MarkStarted(now);
        controlAction.MarkCompleted(ControlActionStatus.Succeeded, JsonSerializer.Serialize(result, JsonOptions), null, now);
        dbContext.ControlActions.Add(controlAction);
    }

    private static WebhookEndpointResponse ToEndpointResponse(
        WebhookEndpoint endpoint,
        string projectName,
        string projectSlug,
        string environmentName,
        string environmentSlug)
    {
        return new WebhookEndpointResponse(
            endpoint.Id,
            endpoint.Name,
            endpoint.Url,
            endpoint.SecretPrefix,
            WebhookEventTypes.FromJson(endpoint.EventTypesJson),
            endpoint.ProjectId,
            projectName,
            projectSlug,
            endpoint.EnvironmentId,
            environmentName,
            environmentSlug,
            endpoint.IsPaused,
            endpoint.CreatedAt,
            endpoint.UpdatedAt,
            endpoint.PausedAt,
            endpoint.LastDeliveryAt,
            endpoint.LastSuccessAt,
            endpoint.LastFailureAt);
    }

    private static WebhookEndpointCreateResponse ToEndpointCreateResponse(
        WebhookEndpoint endpoint,
        string projectName,
        string projectSlug,
        string environmentName,
        string environmentSlug,
        string secret)
    {
        var response = ToEndpointResponse(endpoint, projectName, projectSlug, environmentName, environmentSlug);
        return new WebhookEndpointCreateResponse(
            response.Id,
            response.Name,
            response.Url,
            response.SecretPrefix,
            response.EventTypes,
            response.ProjectId,
            response.ProjectName,
            response.ProjectSlug,
            response.EnvironmentId,
            response.EnvironmentName,
            response.EnvironmentSlug,
            response.IsPaused,
            response.CreatedAt,
            response.UpdatedAt,
            response.PausedAt,
            response.LastDeliveryAt,
            response.LastSuccessAt,
            response.LastFailureAt,
            secret);
    }

    private static IResult? AccessFailure(TenantAccessResult result)
    {
        return result.Status switch
        {
            TenantAccessStatus.Granted => null,
            TenantAccessStatus.Forbidden => Results.Forbid(),
            _ => Results.NotFound()
        };
    }

    private sealed record ScopedEnvironment(Project Project, ProjectEnvironment Environment);

    private sealed record NormalizedWebhookCreateRequest(string Name, string Url, IReadOnlyList<string> EventTypes, IResult? Failure)
    {
        public static NormalizedWebhookCreateRequest Valid(string name, string url, IReadOnlyList<string> eventTypes) => new(name, url, eventTypes, null);

        public static NormalizedWebhookCreateRequest Failed(IResult failure) => new(string.Empty, string.Empty, [], failure);
    }
}

public sealed record WebhookEndpointCreateRequest(string? Name, string? Url, IReadOnlyList<string>? EventTypes);

public sealed record WebhookEndpointResponse(
    Guid Id,
    string Name,
    string Url,
    string SecretPrefix,
    IReadOnlyList<string> EventTypes,
    Guid ProjectId,
    string ProjectName,
    string ProjectSlug,
    Guid EnvironmentId,
    string EnvironmentName,
    string EnvironmentSlug,
    bool IsPaused,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? PausedAt,
    DateTimeOffset? LastDeliveryAt,
    DateTimeOffset? LastSuccessAt,
    DateTimeOffset? LastFailureAt);

public sealed record WebhookEndpointCreateResponse(
    Guid Id,
    string Name,
    string Url,
    string SecretPrefix,
    IReadOnlyList<string> EventTypes,
    Guid ProjectId,
    string ProjectName,
    string ProjectSlug,
    Guid EnvironmentId,
    string EnvironmentName,
    string EnvironmentSlug,
    bool IsPaused,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? PausedAt,
    DateTimeOffset? LastDeliveryAt,
    DateTimeOffset? LastSuccessAt,
    DateTimeOffset? LastFailureAt,
    string Secret);

public sealed record WebhookDeliveryResponse(
    Guid Id,
    Guid EndpointId,
    Guid EventId,
    string EventType,
    string ResourceType,
    string? ResourceId,
    string Status,
    int AttemptCount,
    int MaxAttempts,
    DateTimeOffset? NextAttemptAt,
    DateTimeOffset? LastAttemptAt,
    DateTimeOffset? CompletedAt,
    int? LastStatusCode,
    string LastError,
    string LastResponsePreview,
    bool LastResponseTruncated,
    DateTimeOffset CreatedAt);
