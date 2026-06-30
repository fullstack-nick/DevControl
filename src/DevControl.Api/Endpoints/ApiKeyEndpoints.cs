using System.Diagnostics;
using System.Text.Json;
using DevControl.Api.Security;
using DevControl.Application.Security;
using DevControl.Domain.Entities;
using DevControl.Domain.Enums;
using DevControl.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;

namespace DevControl.Api.Endpoints;

public static class ApiKeyEndpoints
{
    private const string RuntimeSampleEndpoint = "/api/runtime/sample/echo";
    private const int DefaultRateLimitPerMinute = 10;
    private const int MaxRateLimitPerMinute = 600;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static void MapApiKeyEndpoints(this WebApplication app)
    {
        app.MapGet(RuntimeSampleEndpoint, RuntimeSampleEchoAsync);

        var api = app.MapGroup("/api").RequireAuthorization();

        api.MapGet("/organizations/{organizationId:guid}/api-keys", ListApiKeysAsync);
        api.MapPost(
            "/organizations/{organizationId:guid}/projects/{projectId:guid}/environments/{environmentId:guid}/api-keys",
            CreateApiKeyAsync).RequireCsrf();
        api.MapPost(
            "/organizations/{organizationId:guid}/api-keys/{apiKeyId:guid}/revoke",
            RevokeApiKeyAsync).RequireCsrf();
        api.MapPost(
            "/organizations/{organizationId:guid}/api-keys/{apiKeyId:guid}/rotate",
            RotateApiKeyAsync).RequireCsrf();
    }

    private static async Task<IResult> RuntimeSampleEchoAsync(
        HttpContext httpContext,
        RuntimeApiKeyService runtimeApiKeyService,
        TimeProvider timeProvider,
        int? status,
        int? delayMs,
        CancellationToken cancellationToken)
    {
        var auth = await runtimeApiKeyService.AuthenticateAsync(
            httpContext,
            RuntimeSampleEndpoint,
            ApiKeyScopes.SampleRead,
            cancellationToken);

        if (auth.Status != RuntimeApiKeyAuthStatus.Granted || auth.ApiKey is null)
        {
            return auth.Status switch
            {
                RuntimeApiKeyAuthStatus.RateLimited => Results.Problem("API key rate limit exceeded.", statusCode: StatusCodes.Status429TooManyRequests),
                RuntimeApiKeyAuthStatus.ScopeDenied => Results.Forbid(),
                _ => Results.Unauthorized()
            };
        }

        if (status is < 200 or > 599)
        {
            return Results.BadRequest(new ProblemDetailsResponse("Status must be between 200 and 599."));
        }

        if (delayMs is < 0 or > 2000)
        {
            return Results.BadRequest(new ProblemDetailsResponse("delayMs must be between 0 and 2000."));
        }

        var responseStatus = status ?? StatusCodes.Status200OK;
        var boundedDelayMs = delayMs ?? 0;
        var startedAt = Stopwatch.GetTimestamp();

        if (boundedDelayMs > 0)
        {
            await Task.Delay(boundedDelayMs, cancellationToken);
        }

        var elapsed = Stopwatch.GetElapsedTime(startedAt);
        await runtimeApiKeyService.RecordResultAsync(
            auth.ApiKey,
            RuntimeSampleEndpoint,
            responseStatus,
            elapsed,
            cancellationToken);

        return Results.Json(
            new RuntimeSampleEchoResponse(
                "devcontrol-runtime-sample",
                auth.ApiKey.OrganizationId,
                auth.ApiKey.ProjectId,
                auth.ApiKey.EnvironmentId,
                RuntimeSampleEndpoint,
                responseStatus,
                boundedDelayMs,
                timeProvider.GetUtcNow()),
            statusCode: responseStatus);
    }

    private static async Task<IResult> ListApiKeysAsync(
        Guid organizationId,
        CurrentUserAccessor currentUserAccessor,
        TenantAccessService tenantAccess,
        DevControlDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var actor = await currentUserAccessor.GetOrCreateAsync(cancellationToken);
        var access = await tenantAccess.RequireAsync(organizationId, actor, OrganizationRole.Admin, cancellationToken);
        var failure = AccessFailure(access);
        if (failure is not null)
        {
            return failure;
        }

        var apiKeys = await dbContext.ApiKeys
            .Where(apiKey => apiKey.OrganizationId == organizationId)
            .Join(
                dbContext.Projects,
                apiKey => apiKey.ProjectId,
                project => project.Id,
                (apiKey, project) => new { apiKey, project })
            .Join(
                dbContext.ProjectEnvironments,
                candidate => candidate.apiKey.EnvironmentId,
                environment => environment.Id,
                (candidate, environment) => new { candidate.apiKey, candidate.project, environment })
            .OrderByDescending(candidate => candidate.apiKey.CreatedAt)
            .ToListAsync(cancellationToken);

        return Results.Ok(apiKeys.Select(candidate => ToApiKeyResponse(candidate.apiKey, candidate.project, candidate.environment)));
    }

    private static async Task<IResult> CreateApiKeyAsync(
        Guid organizationId,
        Guid projectId,
        Guid environmentId,
        ApiKeyCreateRequest request,
        CurrentUserAccessor currentUserAccessor,
        TenantAccessService tenantAccess,
        DevControlDbContext dbContext,
        AuditLogWriter auditLogWriter,
        ApiKeySecretService apiKeySecretService,
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
            deniedAction: "api_key.create.denied",
            targetType: "api_key");
        var failure = AccessFailure(access);
        if (failure is not null)
        {
            return failure;
        }

        var project = await dbContext.Projects
            .SingleOrDefaultAsync(candidate => candidate.OrganizationId == organizationId && candidate.Id == projectId, cancellationToken);
        var environment = await dbContext.ProjectEnvironments
            .SingleOrDefaultAsync(
                candidate =>
                    candidate.OrganizationId == organizationId &&
                    candidate.ProjectId == projectId &&
                    candidate.Id == environmentId,
                cancellationToken);

        if (project is null || environment is null)
        {
            return Results.NotFound();
        }

        var name = string.IsNullOrWhiteSpace(request.Name)
            ? $"{project.Slug}/{environment.Slug} API key"
            : request.Name.Trim();
        if (name.Length > 160)
        {
            return Results.BadRequest(new ProblemDetailsResponse("API key name cannot exceed 160 characters."));
        }

        var rateLimitPerMinute = request.RateLimitPerMinute ?? DefaultRateLimitPerMinute;
        if (rateLimitPerMinute is < 1 or > MaxRateLimitPerMinute)
        {
            return Results.BadRequest(new ProblemDetailsResponse($"Rate limit must be between 1 and {MaxRateLimitPerMinute} requests per minute."));
        }

        if (!ApiKeyScopes.TryNormalize(request.Scopes, out var scopes, out var scopesJson, out var scopeErrors))
        {
            return Results.BadRequest(new ValidationProblemDetailsResponse(scopeErrors));
        }

        var secret = apiKeySecretService.CreateKey();
        var now = timeProvider.GetUtcNow();
        var apiKey = new ApiKey(
            organizationId,
            projectId,
            environmentId,
            name,
            secret.Prefix,
            secret.Hash,
            scopesJson,
            rateLimitPerMinute,
            actor.Id,
            now);

        dbContext.ApiKeys.Add(apiKey);
        auditLogWriter.Add(
            organizationId,
            actor,
            "api_key.create",
            "Succeeded",
            "api_key",
            apiKey.Id.ToString(),
            "API key created.",
            new { apiKey.Name, apiKey.KeyPrefix, scopes, apiKey.RateLimitPerMinute, projectSlug = project.Slug, environmentSlug = environment.Slug },
            projectId,
            environmentId);

        await dbContext.SaveChangesAsync(cancellationToken);

        return Results.Created(
            $"/api/organizations/{organizationId}/api-keys/{apiKey.Id}",
            ToApiKeyCreateResponse(apiKey, project, environment, secret.Secret));
    }

    private static async Task<IResult> RevokeApiKeyAsync(
        Guid organizationId,
        Guid apiKeyId,
        CurrentUserAccessor currentUserAccessor,
        TenantAccessService tenantAccess,
        DevControlDbContext dbContext,
        AuditLogWriter auditLogWriter,
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
            deniedAction: "api_key.revoke.denied",
            targetType: "api_key",
            targetId: apiKeyId.ToString());
        var failure = AccessFailure(access);
        if (failure is not null)
        {
            return failure;
        }

        var apiKey = await dbContext.ApiKeys
            .SingleOrDefaultAsync(candidate => candidate.OrganizationId == organizationId && candidate.Id == apiKeyId, cancellationToken);
        if (apiKey is null)
        {
            return Results.NotFound();
        }

        var now = timeProvider.GetUtcNow();
        apiKey.Revoke(actor.Id, now);
        AddCompletedControlAction(dbContext, organizationId, actor, apiKey, "api_key.revoke", now, new { apiKey.Id, apiKey.KeyPrefix });
        auditLogWriter.Add(
            organizationId,
            actor,
            "api_key.revoke",
            "Succeeded",
            "api_key",
            apiKey.Id.ToString(),
            "API key revoked.",
            new { apiKey.Name, apiKey.KeyPrefix },
            apiKey.ProjectId,
            apiKey.EnvironmentId);

        await dbContext.SaveChangesAsync(cancellationToken);
        return Results.Ok(new ApiKeyRevokeResponse(apiKey.Id, apiKey.RevokedAt));
    }

    private static async Task<IResult> RotateApiKeyAsync(
        Guid organizationId,
        Guid apiKeyId,
        CurrentUserAccessor currentUserAccessor,
        TenantAccessService tenantAccess,
        DevControlDbContext dbContext,
        AuditLogWriter auditLogWriter,
        ApiKeySecretService apiKeySecretService,
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
            deniedAction: "api_key.rotate.denied",
            targetType: "api_key",
            targetId: apiKeyId.ToString());
        var failure = AccessFailure(access);
        if (failure is not null)
        {
            return failure;
        }

        var apiKey = await dbContext.ApiKeys
            .SingleOrDefaultAsync(candidate => candidate.OrganizationId == organizationId && candidate.Id == apiKeyId, cancellationToken);
        if (apiKey is null)
        {
            return Results.NotFound();
        }

        if (apiKey.IsRevoked)
        {
            return Results.BadRequest(new ProblemDetailsResponse("Revoked API keys cannot be rotated."));
        }

        var project = await dbContext.Projects
            .SingleAsync(candidate => candidate.OrganizationId == organizationId && candidate.Id == apiKey.ProjectId, cancellationToken);
        var environment = await dbContext.ProjectEnvironments
            .SingleAsync(candidate => candidate.OrganizationId == organizationId && candidate.Id == apiKey.EnvironmentId, cancellationToken);

        var secret = apiKeySecretService.CreateKey();
        var now = timeProvider.GetUtcNow();
        var rotated = new ApiKey(
            organizationId,
            apiKey.ProjectId,
            apiKey.EnvironmentId,
            apiKey.Name,
            secret.Prefix,
            secret.Hash,
            apiKey.ScopesJson,
            apiKey.RateLimitPerMinute,
            actor.Id,
            now,
            apiKey.Id);

        dbContext.ApiKeys.Add(rotated);
        apiKey.MarkRotated(actor.Id, rotated.Id, now);
        AddCompletedControlAction(dbContext, organizationId, actor, apiKey, "api_key.rotate", now, new { oldApiKeyId = apiKey.Id, newApiKeyId = rotated.Id });
        auditLogWriter.Add(
            organizationId,
            actor,
            "api_key.rotate",
            "Succeeded",
            "api_key",
            apiKey.Id.ToString(),
            "API key rotated.",
            new { oldKeyPrefix = apiKey.KeyPrefix, newKeyPrefix = rotated.KeyPrefix, scopes = ApiKeyScopes.FromJson(apiKey.ScopesJson) },
            apiKey.ProjectId,
            apiKey.EnvironmentId);

        await dbContext.SaveChangesAsync(cancellationToken);
        return Results.Ok(ToApiKeyCreateResponse(rotated, project, environment, secret.Secret));
    }

    private static void AddCompletedControlAction(
        DevControlDbContext dbContext,
        Guid organizationId,
        CurrentUser actor,
        ApiKey apiKey,
        string actionType,
        DateTimeOffset now,
        object result)
    {
        var controlAction = new ControlAction(
            organizationId,
            apiKey.ProjectId,
            apiKey.EnvironmentId,
            actionType,
            actor.Id,
            "api_key",
            apiKey.Id.ToString(),
            JsonSerializer.Serialize(new { apiKey.Id, apiKey.KeyPrefix }, JsonOptions),
            now);
        controlAction.MarkStarted(now);
        controlAction.MarkCompleted(ControlActionStatus.Succeeded, JsonSerializer.Serialize(result, JsonOptions), null, now);
        dbContext.ControlActions.Add(controlAction);
    }

    private static ApiKeyResponse ToApiKeyResponse(ApiKey apiKey, Project project, ProjectEnvironment environment)
    {
        return new ApiKeyResponse(
            apiKey.Id,
            apiKey.Name,
            apiKey.KeyPrefix,
            ApiKeyScopes.FromJson(apiKey.ScopesJson),
            apiKey.RateLimitPerMinute,
            project.Id,
            project.Name,
            project.Slug,
            environment.Id,
            environment.Name,
            environment.Slug,
            apiKey.CreatedAt,
            apiKey.LastUsedAt,
            apiKey.RevokedAt,
            apiKey.RotatedAt,
            apiKey.RotatedFromApiKeyId,
            apiKey.RotatedToApiKeyId,
            apiKey.TotalRequestCount,
            apiKey.FailureCount,
            AverageLatency(apiKey),
            apiKey.RateLimitHitCount);
    }

    private static ApiKeyCreateResponse ToApiKeyCreateResponse(ApiKey apiKey, Project project, ProjectEnvironment environment, string secret)
    {
        var response = ToApiKeyResponse(apiKey, project, environment);
        return new ApiKeyCreateResponse(
            response.Id,
            response.Name,
            response.KeyPrefix,
            response.Scopes,
            response.RateLimitPerMinute,
            response.ProjectId,
            response.ProjectName,
            response.ProjectSlug,
            response.EnvironmentId,
            response.EnvironmentName,
            response.EnvironmentSlug,
            response.CreatedAt,
            response.LastUsedAt,
            response.RevokedAt,
            response.RotatedAt,
            response.RotatedFromApiKeyId,
            response.RotatedToApiKeyId,
            response.TotalRequestCount,
            response.FailureCount,
            response.AverageLatencyMilliseconds,
            response.RateLimitHitCount,
            secret);
    }

    private static double AverageLatency(ApiKey apiKey)
    {
        return apiKey.LatencySampleCount == 0
            ? 0
            : Math.Round((double)apiKey.TotalLatencyMilliseconds / apiKey.LatencySampleCount, 2);
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
}

public sealed record ApiKeyCreateRequest(string? Name, IReadOnlyList<string>? Scopes, int? RateLimitPerMinute);

public sealed record ApiKeyResponse(
    Guid Id,
    string Name,
    string KeyPrefix,
    IReadOnlyList<string> Scopes,
    int RateLimitPerMinute,
    Guid ProjectId,
    string ProjectName,
    string ProjectSlug,
    Guid EnvironmentId,
    string EnvironmentName,
    string EnvironmentSlug,
    DateTimeOffset CreatedAt,
    DateTimeOffset? LastUsedAt,
    DateTimeOffset? RevokedAt,
    DateTimeOffset? RotatedAt,
    Guid? RotatedFromApiKeyId,
    Guid? RotatedToApiKeyId,
    long TotalRequestCount,
    long FailureCount,
    double AverageLatencyMilliseconds,
    long RateLimitHitCount);

public sealed record ApiKeyCreateResponse(
    Guid Id,
    string Name,
    string KeyPrefix,
    IReadOnlyList<string> Scopes,
    int RateLimitPerMinute,
    Guid ProjectId,
    string ProjectName,
    string ProjectSlug,
    Guid EnvironmentId,
    string EnvironmentName,
    string EnvironmentSlug,
    DateTimeOffset CreatedAt,
    DateTimeOffset? LastUsedAt,
    DateTimeOffset? RevokedAt,
    DateTimeOffset? RotatedAt,
    Guid? RotatedFromApiKeyId,
    Guid? RotatedToApiKeyId,
    long TotalRequestCount,
    long FailureCount,
    double AverageLatencyMilliseconds,
    long RateLimitHitCount,
    string Secret);

public sealed record ApiKeyRevokeResponse(Guid Id, DateTimeOffset? RevokedAt);

public sealed record RuntimeSampleEchoResponse(
    string Service,
    Guid OrganizationId,
    Guid ProjectId,
    Guid EnvironmentId,
    string Endpoint,
    int Status,
    int DelayMs,
    DateTimeOffset TimestampUtc);
