using System.Diagnostics;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using DevControl.Api.Security;
using DevControl.Application.Security;
using DevControl.Domain.Entities;
using DevControl.Domain.Enums;
using DevControl.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;

namespace DevControl.Api.Endpoints;

public static class FeatureFlagEndpoints
{
    private const string RuntimeSnapshotEndpoint = "/api/runtime/flags/snapshot";
    private const int SnapshotRefreshIntervalSeconds = 60;
    private const int KillSwitchRefreshIntervalSeconds = 20;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static void MapFeatureFlagEndpoints(this WebApplication app)
    {
        app.MapGet(RuntimeSnapshotEndpoint, GetRuntimeSnapshotAsync);

        var api = app.MapGroup("/api").RequireAuthorization();

        api.MapGet(
            "/organizations/{organizationId:guid}/projects/{projectId:guid}/environments/{environmentId:guid}/feature-flags",
            ListFeatureFlagsAsync);
        api.MapPost(
            "/organizations/{organizationId:guid}/projects/{projectId:guid}/environments/{environmentId:guid}/feature-flags",
            CreateFeatureFlagAsync).RequireCsrf();
        api.MapPatch(
            "/organizations/{organizationId:guid}/feature-flags/{featureFlagId:guid}",
            UpdateFeatureFlagAsync).RequireCsrf();
        api.MapGet(
            "/organizations/{organizationId:guid}/feature-flags/{featureFlagId:guid}/changes",
            ListFeatureFlagChangesAsync);
    }

    private static async Task<IResult> GetRuntimeSnapshotAsync(
        HttpContext httpContext,
        RuntimeApiKeyService runtimeApiKeyService,
        DevControlDbContext dbContext,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var auth = await runtimeApiKeyService.AuthenticateAsync(
            httpContext,
            RuntimeSnapshotEndpoint,
            ApiKeyScopes.FlagsRead,
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

        var startedAt = Stopwatch.GetTimestamp();
        var flags = await dbContext.FeatureFlags
            .Where(flag =>
                flag.OrganizationId == auth.ApiKey.OrganizationId &&
                flag.ProjectId == auth.ApiKey.ProjectId &&
                flag.EnvironmentId == auth.ApiKey.EnvironmentId)
            .OrderBy(flag => flag.Key)
            .ToListAsync(cancellationToken);

        var version = BuildSnapshotVersion(flags);
        var etag = QuoteETag(version);
        httpContext.Response.Headers.ETag = etag;
        httpContext.Response.Headers.CacheControl = "private, no-cache";

        if (RequestHasMatchingETag(httpContext.Request, etag))
        {
            await runtimeApiKeyService.RecordResultAsync(
                auth.ApiKey,
                RuntimeSnapshotEndpoint,
                StatusCodes.Status304NotModified,
                Stopwatch.GetElapsedTime(startedAt),
                cancellationToken);
            return Results.StatusCode(StatusCodes.Status304NotModified);
        }

        var response = new FlagSnapshotResponse(
            version,
            timeProvider.GetUtcNow(),
            SnapshotRefreshIntervalSeconds,
            KillSwitchRefreshIntervalSeconds,
            flags
                .Where(flag => flag.Kind == FeatureFlagKind.FeatureFlag)
                .ToDictionary(flag => flag.Key, flag => flag.IsEnabled, StringComparer.Ordinal),
            flags
                .Where(flag => flag.Kind == FeatureFlagKind.KillSwitch)
                .ToDictionary(flag => flag.Key, flag => flag.IsEnabled, StringComparer.Ordinal));

        await runtimeApiKeyService.RecordResultAsync(
            auth.ApiKey,
            RuntimeSnapshotEndpoint,
            StatusCodes.Status200OK,
            Stopwatch.GetElapsedTime(startedAt),
            cancellationToken);

        return Results.Ok(response);
    }

    private static async Task<IResult> ListFeatureFlagsAsync(
        Guid organizationId,
        Guid projectId,
        Guid environmentId,
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

        var flags = await dbContext.FeatureFlags
            .Where(flag =>
                flag.OrganizationId == organizationId &&
                flag.ProjectId == projectId &&
                flag.EnvironmentId == environmentId)
            .OrderBy(flag => flag.Kind)
            .ThenBy(flag => flag.Key)
            .ToListAsync(cancellationToken);

        return Results.Ok(flags.Select(flag => ToFeatureFlagResponse(flag, project, environment)));
    }

    private static async Task<IResult> CreateFeatureFlagAsync(
        Guid organizationId,
        Guid projectId,
        Guid environmentId,
        FeatureFlagCreateRequest request,
        CurrentUserAccessor currentUserAccessor,
        TenantAccessService tenantAccess,
        DevControlDbContext dbContext,
        AuditLogWriter auditLogWriter,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var actor = await currentUserAccessor.GetOrCreateAsync(cancellationToken);
        var scope = await LoadScopedEnvironmentAsync(dbContext, organizationId, projectId, environmentId, cancellationToken);
        if (scope is null)
        {
            return Results.NotFound();
        }

        var access = await RequireMutationAccessAsync(
            organizationId,
            actor,
            scope.Environment,
            tenantAccess,
            auditLogWriter,
            dbContext,
            "feature_flag.create.denied",
            "feature_flag",
            null,
            cancellationToken);
        if (access is not null)
        {
            return access;
        }

        if (!FeatureFlagKeys.TryNormalize(request.Key, out var key, out var keyError))
        {
            return Results.BadRequest(new ProblemDetailsResponse(keyError!));
        }

        if (!TryNormalizeKind(request.Kind, out var kind, out var kindError))
        {
            return Results.BadRequest(new ProblemDetailsResponse(kindError!));
        }

        var validation = ValidateUpsert(request.Name, request.Description, request.Reason, scope.Environment, out var name, out var description, out var reason);
        if (validation is not null)
        {
            return validation;
        }

        if (await dbContext.FeatureFlags.AnyAsync(
                flag =>
                    flag.OrganizationId == organizationId &&
                    flag.ProjectId == projectId &&
                    flag.EnvironmentId == environmentId &&
                    flag.Key == key,
                cancellationToken))
        {
            return Results.Conflict(new ProblemDetailsResponse("Flag key is already in use for this environment."));
        }

        var now = timeProvider.GetUtcNow();
        var flag = new FeatureFlag(
            organizationId,
            projectId,
            environmentId,
            key,
            string.IsNullOrWhiteSpace(name) ? key : name,
            description,
            kind,
            request.Enabled,
            actor.Id,
            now);

        dbContext.FeatureFlags.Add(flag);
        dbContext.FeatureFlagChanges.Add(new FeatureFlagChange(
            flag.Id,
            organizationId,
            projectId,
            environmentId,
            oldValue: false,
            request.Enabled,
            reason,
            actor.Id,
            now));
        AddCompletedControlAction(dbContext, organizationId, actor, flag, "feature_flag.create", now, new { flag.Id, flag.Key, flag.Kind, flag.IsEnabled });
        auditLogWriter.Add(
            organizationId,
            actor,
            "feature_flag.create",
            "Succeeded",
            "feature_flag",
            flag.Id.ToString(),
            "Feature flag created.",
            new { flag.Key, flag.Name, flag.Kind, flag.IsEnabled, reason },
            projectId,
            environmentId);

        await dbContext.SaveChangesAsync(cancellationToken);
        return Results.Created(
            $"/api/organizations/{organizationId}/feature-flags/{flag.Id}",
            ToFeatureFlagResponse(flag, scope.Project, scope.Environment));
    }

    private static async Task<IResult> UpdateFeatureFlagAsync(
        Guid organizationId,
        Guid featureFlagId,
        FeatureFlagUpdateRequest request,
        CurrentUserAccessor currentUserAccessor,
        TenantAccessService tenantAccess,
        DevControlDbContext dbContext,
        AuditLogWriter auditLogWriter,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var actor = await currentUserAccessor.GetOrCreateAsync(cancellationToken);
        var flag = await dbContext.FeatureFlags
            .SingleOrDefaultAsync(candidate => candidate.OrganizationId == organizationId && candidate.Id == featureFlagId, cancellationToken);
        if (flag is null)
        {
            return Results.NotFound();
        }

        var scope = await LoadScopedEnvironmentAsync(dbContext, organizationId, flag.ProjectId, flag.EnvironmentId, cancellationToken);
        if (scope is null)
        {
            return Results.NotFound();
        }

        var access = await RequireMutationAccessAsync(
            organizationId,
            actor,
            scope.Environment,
            tenantAccess,
            auditLogWriter,
            dbContext,
            "feature_flag.update.denied",
            "feature_flag",
            flag.Id.ToString(),
            cancellationToken);
        if (access is not null)
        {
            return access;
        }

        var validation = ValidateUpsert(
            request.Name ?? flag.Name,
            request.Description ?? flag.Description,
            request.Reason,
            scope.Environment,
            out var name,
            out var description,
            out var reason);
        if (validation is not null)
        {
            return validation;
        }

        var oldValue = flag.IsEnabled;
        var nextEnabled = request.Enabled ?? flag.IsEnabled;
        var now = timeProvider.GetUtcNow();
        flag.Update(name, description, nextEnabled, actor.Id, now);
        dbContext.FeatureFlagChanges.Add(new FeatureFlagChange(
            flag.Id,
            organizationId,
            flag.ProjectId,
            flag.EnvironmentId,
            oldValue,
            nextEnabled,
            reason,
            actor.Id,
            now));
        AddCompletedControlAction(dbContext, organizationId, actor, flag, "feature_flag.update", now, new { flag.Id, flag.Key, flag.Kind, oldValue, newValue = nextEnabled });
        auditLogWriter.Add(
            organizationId,
            actor,
            "feature_flag.update",
            "Succeeded",
            "feature_flag",
            flag.Id.ToString(),
            "Feature flag updated.",
            new { flag.Key, flag.Name, flag.Kind, oldValue, newValue = nextEnabled, reason },
            flag.ProjectId,
            flag.EnvironmentId);

        await dbContext.SaveChangesAsync(cancellationToken);
        return Results.Ok(ToFeatureFlagResponse(flag, scope.Project, scope.Environment));
    }

    private static async Task<IResult> ListFeatureFlagChangesAsync(
        Guid organizationId,
        Guid featureFlagId,
        CurrentUserAccessor currentUserAccessor,
        TenantAccessService tenantAccess,
        DevControlDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var actor = await currentUserAccessor.GetOrCreateAsync(cancellationToken);
        var access = await tenantAccess.RequireAsync(organizationId, actor, OrganizationRole.Developer, cancellationToken);
        var failure = AccessFailure(access);
        if (failure is not null)
        {
            return failure;
        }

        var flagExists = await dbContext.FeatureFlags
            .AnyAsync(candidate => candidate.OrganizationId == organizationId && candidate.Id == featureFlagId, cancellationToken);
        if (!flagExists)
        {
            return Results.NotFound();
        }

        var changes = await dbContext.FeatureFlagChanges
            .Where(change => change.OrganizationId == organizationId && change.FeatureFlagId == featureFlagId)
            .Join(
                dbContext.Users,
                change => change.ChangedByUserId,
                user => user.Id,
                (change, user) => new { change, user })
            .OrderByDescending(candidate => candidate.change.ChangedAt)
            .Take(50)
            .Select(candidate => new FeatureFlagChangeResponse(
                candidate.change.Id,
                candidate.change.FeatureFlagId,
                candidate.change.OldValue,
                candidate.change.NewValue,
                candidate.change.Reason,
                candidate.user.Email,
                candidate.change.ChangedAt))
            .ToListAsync(cancellationToken);

        return Results.Ok(changes);
    }

    private static async Task<IResult?> RequireMutationAccessAsync(
        Guid organizationId,
        CurrentUser actor,
        ProjectEnvironment environment,
        TenantAccessService tenantAccess,
        AuditLogWriter auditLogWriter,
        DevControlDbContext dbContext,
        string deniedAction,
        string targetType,
        string? targetId,
        CancellationToken cancellationToken)
    {
        var requiredRole = IsProduction(environment) ? OrganizationRole.Admin : OrganizationRole.Developer;
        var access = await tenantAccess.RequireAsync(organizationId, actor, OrganizationRole.Viewer, cancellationToken);
        var failure = AccessFailure(access);
        if (failure is not null)
        {
            return failure;
        }

        var role = access.Access!.Member.Role;
        if (RolePermissions.AtLeast(role, requiredRole))
        {
            return null;
        }

        auditLogWriter.Add(
            organizationId,
            actor,
            deniedAction,
            "Denied",
            targetType,
            targetId,
            $"Denied feature flag change because {role} is below required role {requiredRole}.",
            new { role, requiredRole, environmentSlug = environment.Slug },
            environment.ProjectId,
            environment.Id);
        await dbContext.SaveChangesAsync(cancellationToken);
        return Results.Forbid();
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

    private static IResult? ValidateUpsert(
        string? requestedName,
        string? requestedDescription,
        string? requestedReason,
        ProjectEnvironment environment,
        out string name,
        out string description,
        out string reason)
    {
        name = requestedName?.Trim() ?? string.Empty;
        description = requestedDescription?.Trim() ?? string.Empty;
        reason = requestedReason?.Trim() ?? string.Empty;

        if (name.Length > 160)
        {
            return Results.BadRequest(new ProblemDetailsResponse("Flag name cannot exceed 160 characters."));
        }

        if (description.Length > 1000)
        {
            return Results.BadRequest(new ProblemDetailsResponse("Flag description cannot exceed 1000 characters."));
        }

        if (reason.Length > 1000)
        {
            return Results.BadRequest(new ProblemDetailsResponse("Flag change reason cannot exceed 1000 characters."));
        }

        if (IsProduction(environment) && string.IsNullOrWhiteSpace(reason))
        {
            return Results.BadRequest(new ProblemDetailsResponse("Production flag changes require a reason."));
        }

        return null;
    }

    private static bool TryNormalizeKind(string? value, out FeatureFlagKind kind, out string? error)
    {
        var normalized = (value ?? string.Empty).Trim();
        if (normalized.Equals("FeatureFlag", StringComparison.OrdinalIgnoreCase) ||
            normalized.Equals("feature-flag", StringComparison.OrdinalIgnoreCase) ||
            normalized.Equals("flag", StringComparison.OrdinalIgnoreCase))
        {
            kind = FeatureFlagKind.FeatureFlag;
            error = null;
            return true;
        }

        if (normalized.Equals("KillSwitch", StringComparison.OrdinalIgnoreCase) ||
            normalized.Equals("kill-switch", StringComparison.OrdinalIgnoreCase) ||
            normalized.Equals("kill", StringComparison.OrdinalIgnoreCase))
        {
            kind = FeatureFlagKind.KillSwitch;
            error = null;
            return true;
        }

        kind = FeatureFlagKind.FeatureFlag;
        error = "Flag kind must be FeatureFlag or KillSwitch.";
        return false;
    }

    private static bool IsProduction(ProjectEnvironment environment)
    {
        return environment.Slug.Equals("production", StringComparison.OrdinalIgnoreCase);
    }

    private static void AddCompletedControlAction(
        DevControlDbContext dbContext,
        Guid organizationId,
        CurrentUser actor,
        FeatureFlag flag,
        string actionType,
        DateTimeOffset now,
        object result)
    {
        var controlAction = new ControlAction(
            organizationId,
            flag.ProjectId,
            flag.EnvironmentId,
            actionType,
            actor.Id,
            "feature_flag",
            flag.Id.ToString(),
            JsonSerializer.Serialize(new { flag.Id, flag.Key, flag.Kind }, JsonOptions),
            now);
        controlAction.MarkStarted(now);
        controlAction.MarkCompleted(ControlActionStatus.Succeeded, JsonSerializer.Serialize(result, JsonOptions), null, now);
        dbContext.ControlActions.Add(controlAction);
    }

    private static FeatureFlagResponse ToFeatureFlagResponse(FeatureFlag flag, Project project, ProjectEnvironment environment)
    {
        return new FeatureFlagResponse(
            flag.Id,
            flag.Key,
            flag.Name,
            flag.Description,
            flag.Kind.ToString(),
            flag.IsEnabled,
            project.Id,
            project.Name,
            project.Slug,
            environment.Id,
            environment.Name,
            environment.Slug,
            flag.CreatedAt,
            flag.UpdatedAt,
            flag.LastChangedAt);
    }

    private static string BuildSnapshotVersion(IReadOnlyList<FeatureFlag> flags)
    {
        var builder = new StringBuilder();
        foreach (var flag in flags.OrderBy(flag => flag.Key, StringComparer.Ordinal))
        {
            builder
                .Append(flag.Key)
                .Append('|')
                .Append(flag.Kind)
                .Append('|')
                .Append(flag.IsEnabled)
                .Append('|')
                .Append(flag.UpdatedAt.UtcTicks)
                .AppendLine();
        }

        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(builder.ToString()));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static string QuoteETag(string version)
    {
        return $"\"{version}\"";
    }

    private static bool RequestHasMatchingETag(HttpRequest request, string etag)
    {
        return request.Headers.IfNoneMatch.Any(value => value?.Equals(etag, StringComparison.Ordinal) == true);
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
}

public sealed record FeatureFlagCreateRequest(string? Key, string? Name, string? Description, string? Kind, bool Enabled, string? Reason);

public sealed record FeatureFlagUpdateRequest(string? Name, string? Description, bool? Enabled, string? Reason);

public sealed record FeatureFlagResponse(
    Guid Id,
    string Key,
    string Name,
    string Description,
    string Kind,
    bool Enabled,
    Guid ProjectId,
    string ProjectName,
    string ProjectSlug,
    Guid EnvironmentId,
    string EnvironmentName,
    string EnvironmentSlug,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    DateTimeOffset LastChangedAt);

public sealed record FeatureFlagChangeResponse(
    Guid Id,
    Guid FeatureFlagId,
    bool OldValue,
    bool NewValue,
    string Reason,
    string ChangedByEmail,
    DateTimeOffset ChangedAt);

public sealed record FlagSnapshotResponse(
    string Version,
    DateTimeOffset GeneratedAt,
    int RefreshIntervalSeconds,
    int KillSwitchRefreshIntervalSeconds,
    IReadOnlyDictionary<string, bool> Flags,
    IReadOnlyDictionary<string, bool> KillSwitches);
