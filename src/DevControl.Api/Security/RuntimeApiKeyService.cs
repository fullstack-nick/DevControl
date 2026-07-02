using DevControl.Api.Observability;
using DevControl.Application.Security;
using DevControl.Domain.Entities;
using DevControl.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;

namespace DevControl.Api.Security;

public enum RuntimeApiKeyAuthStatus
{
    Granted,
    MissingOrInvalid,
    Revoked,
    ScopeDenied,
    RateLimited
}

public sealed record RuntimeApiKeyAuthResult(RuntimeApiKeyAuthStatus Status, ApiKey? ApiKey)
{
    public static RuntimeApiKeyAuthResult Granted(ApiKey apiKey) => new(RuntimeApiKeyAuthStatus.Granted, apiKey);

    public static RuntimeApiKeyAuthResult Denied(RuntimeApiKeyAuthStatus status, ApiKey? apiKey = null) => new(status, apiKey);
}

public sealed class RuntimeApiKeyService(
    DevControlDbContext dbContext,
    ApiKeySecretService apiKeySecretService,
    TimeProvider timeProvider)
{
    private const string ApiKeyHeader = "X-DevControl-Api-Key";

    public async Task<RuntimeApiKeyAuthResult> AuthenticateAsync(
        HttpContext httpContext,
        string endpoint,
        string requiredScope,
        CancellationToken cancellationToken)
    {
        var rawKey = GetRawApiKey(httpContext);
        if (rawKey is null)
        {
            DevControlMetrics.RecordRuntimeApiKeyRequest(endpoint, "missing_or_invalid");
            return RuntimeApiKeyAuthResult.Denied(RuntimeApiKeyAuthStatus.MissingOrInvalid);
        }

        var keyHash = apiKeySecretService.HashKey(rawKey);
        var apiKey = await dbContext.ApiKeys
            .SingleOrDefaultAsync(candidate => candidate.KeyHash == keyHash, cancellationToken);

        if (apiKey is null)
        {
            DevControlMetrics.RecordRuntimeApiKeyRequest(endpoint, "missing_or_invalid");
            return RuntimeApiKeyAuthResult.Denied(RuntimeApiKeyAuthStatus.MissingOrInvalid);
        }

        var now = timeProvider.GetUtcNow();
        if (apiKey.IsRevoked)
        {
            await RecordUsageAsync(apiKey, endpoint, failed: true, latencyMilliseconds: null, rateLimitHit: false, now, cancellationToken);
            DevControlMetrics.RecordRuntimeApiKeyRequest(endpoint, "revoked");
            return RuntimeApiKeyAuthResult.Denied(RuntimeApiKeyAuthStatus.Revoked, apiKey);
        }

        var scopes = ApiKeyScopes.FromJson(apiKey.ScopesJson);
        if (!scopes.Contains(requiredScope, StringComparer.Ordinal))
        {
            await RecordUsageAsync(apiKey, endpoint, failed: true, latencyMilliseconds: null, rateLimitHit: false, now, cancellationToken);
            DevControlMetrics.RecordRuntimeApiKeyRequest(endpoint, "scope_denied");
            return RuntimeApiKeyAuthResult.Denied(RuntimeApiKeyAuthStatus.ScopeDenied, apiKey);
        }

        var windowStart = TruncateToMinute(now);
        var window = await dbContext.ApiKeyRateLimitWindows
            .SingleOrDefaultAsync(
                candidate =>
                    candidate.ApiKeyId == apiKey.Id &&
                    candidate.Endpoint == endpoint &&
                    candidate.WindowStart == windowStart,
                cancellationToken);

        if (window is null)
        {
            window = new ApiKeyRateLimitWindow(apiKey.Id, endpoint, windowStart, now);
            dbContext.ApiKeyRateLimitWindows.Add(window);
        }

        if (window.RequestCount >= apiKey.RateLimitPerMinute)
        {
            window.MarkRateLimitHit(now);
            await RecordUsageAsync(apiKey, endpoint, failed: true, latencyMilliseconds: null, rateLimitHit: true, now, cancellationToken);
            DevControlMetrics.RecordRuntimeApiKeyRequest(endpoint, "rate_limited");
            DevControlMetrics.RecordRuntimeApiKeyRateLimitHit(endpoint);
            return RuntimeApiKeyAuthResult.Denied(RuntimeApiKeyAuthStatus.RateLimited, apiKey);
        }

        window.Increment(now);
        return RuntimeApiKeyAuthResult.Granted(apiKey);
    }

    public async Task RecordResultAsync(
        ApiKey apiKey,
        string endpoint,
        int statusCode,
        TimeSpan elapsed,
        CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var latencyMilliseconds = Math.Clamp((int)Math.Round(elapsed.TotalMilliseconds), 0, int.MaxValue);
        await RecordUsageAsync(apiKey, endpoint, statusCode >= 400, latencyMilliseconds, rateLimitHit: false, now, cancellationToken);
        DevControlMetrics.RecordRuntimeApiKeyRequest(endpoint, $"status_{statusCode}");
    }

    private async Task RecordUsageAsync(
        ApiKey apiKey,
        string endpoint,
        bool failed,
        int? latencyMilliseconds,
        bool rateLimitHit,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        apiKey.RecordUsage(failed, latencyMilliseconds, rateLimitHit, now);

        var day = DateOnly.FromDateTime(now.UtcDateTime);
        var daily = await dbContext.ApiKeyUsageDaily
            .SingleOrDefaultAsync(
                candidate =>
                    candidate.ApiKeyId == apiKey.Id &&
                    candidate.Endpoint == endpoint &&
                    candidate.Day == day,
                cancellationToken);

        if (daily is null)
        {
            daily = new ApiKeyUsageDaily(
                apiKey.Id,
                apiKey.OrganizationId,
                apiKey.ProjectId,
                apiKey.EnvironmentId,
                day,
                endpoint,
                now);
            dbContext.ApiKeyUsageDaily.Add(daily);
        }

        daily.RecordUsage(failed, latencyMilliseconds, rateLimitHit, now);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static DateTimeOffset TruncateToMinute(DateTimeOffset value)
    {
        var utc = value.ToUniversalTime();
        return new DateTimeOffset(utc.Year, utc.Month, utc.Day, utc.Hour, utc.Minute, 0, TimeSpan.Zero);
    }

    private static string? GetRawApiKey(HttpContext httpContext)
    {
        var authorization = httpContext.Request.Headers.Authorization.ToString();
        if (authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            var bearer = authorization["Bearer ".Length..].Trim();
            if (!string.IsNullOrWhiteSpace(bearer))
            {
                return bearer;
            }
        }

        var header = httpContext.Request.Headers[ApiKeyHeader].ToString();
        return string.IsNullOrWhiteSpace(header) ? null : header.Trim();
    }
}
