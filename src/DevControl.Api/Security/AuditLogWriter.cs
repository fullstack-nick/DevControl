using System.Text.Json;
using DevControl.Domain.Entities;
using DevControl.Infrastructure.Database;

namespace DevControl.Api.Security;

public sealed class AuditLogWriter(DevControlDbContext dbContext, IHttpContextAccessor httpContextAccessor, TimeProvider timeProvider)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public void Add(
        Guid organizationId,
        CurrentUser? actor,
        string action,
        string outcome,
        string targetType,
        string? targetId,
        string message,
        object? metadata = null,
        Guid? projectId = null,
        Guid? environmentId = null)
    {
        var httpContext = httpContextAccessor.HttpContext;
        var metadataJson = metadata is null ? "{}" : JsonSerializer.Serialize(metadata, JsonOptions);

        dbContext.AuditLogs.Add(new AuditLog(
            organizationId,
            projectId,
            environmentId,
            actor?.Id,
            actor?.Email ?? string.Empty,
            action,
            outcome,
            targetType,
            targetId,
            message,
            metadataJson,
            httpContext?.Connection.RemoteIpAddress?.ToString() ?? string.Empty,
            httpContext?.Request.Headers.UserAgent.ToString() ?? string.Empty,
            timeProvider.GetUtcNow()));
    }
}
