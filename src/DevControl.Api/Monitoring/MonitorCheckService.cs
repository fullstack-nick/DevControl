using DevControl.Application.Outbound;
using DevControl.Domain.Entities;
using DevControl.Domain.Enums;
using DevControl.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;

namespace DevControl.Api.Monitoring;

public sealed class MonitorCheckService(
    DevControlDbContext dbContext,
    ISafeOutboundHttpClient outboundHttpClient,
    IncidentAutomationService incidentAutomationService,
    TimeProvider timeProvider)
{
    public async Task<MonitorCheckBatchResult> ProcessDueChecksAsync(int batchSize, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var leaseId = Guid.NewGuid().ToString("N");
        var leaseExpiresAt = now.AddMinutes(2);
        var due = await dbContext.UptimeMonitors
            .Where(monitor =>
                !monitor.IsPaused &&
                monitor.NextCheckAt <= now &&
                (monitor.ProcessingLeaseExpiresAt == null || monitor.ProcessingLeaseExpiresAt <= now))
            .OrderBy(monitor => monitor.NextCheckAt)
            .Take(batchSize)
            .ToListAsync(cancellationToken);

        foreach (var monitor in due)
        {
            monitor.Lease(leaseId, leaseExpiresAt, now);
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        var processed = 0;
        foreach (var monitor in due)
        {
            await CheckAsync(monitor.Id, cancellationToken);
            processed++;
        }

        return new MonitorCheckBatchResult(processed, batchSize);
    }

    public async Task<MonitorCheck?> CheckAsync(Guid monitorId, CancellationToken cancellationToken)
    {
        var monitor = await dbContext.UptimeMonitors
            .SingleOrDefaultAsync(candidate => candidate.Id == monitorId, cancellationToken);
        if (monitor is null || monitor.IsPaused)
        {
            return null;
        }

        if (!Uri.TryCreate(monitor.Url, UriKind.Absolute, out var uri))
        {
            return await RecordSyntheticFailureAsync(monitor, "Monitor URL is invalid.", cancellationToken);
        }

        var policy = new OutboundRequestPolicy(
            RequireHttps: false,
            AllowedPorts: OutboundRequestPolicy.Monitor.AllowedPorts,
            Timeout: TimeSpan.FromSeconds(monitor.TimeoutSeconds),
            MaxPreviewBytes: OutboundRequestPolicy.Monitor.MaxPreviewBytes,
            MaxResponseBytes: OutboundRequestPolicy.Monitor.MaxResponseBytes,
            MaxRedirects: OutboundRequestPolicy.Monitor.MaxRedirects);

        var response = await outboundHttpClient.SendAsync(
            new SafeOutboundRequest(
                uri,
                HttpMethod.Get,
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["User-Agent"] = "DevControl-Monitors/1.0"
                },
                Body: null,
                ContentType: "text/plain",
                policy),
            cancellationToken);

        var durationMilliseconds = (long)Math.Round(response.Duration.TotalMilliseconds);
        var status = Classify(response, durationMilliseconds, monitor.SlowThresholdMilliseconds);
        var error = response.Error ?? (response.IsSuccess ? null : response.StatusCode is null
            ? $"Monitor check failed with {response.Kind}."
            : $"Monitor target returned HTTP {(int)response.StatusCode.Value}.");
        return await RecordAsync(
            monitor,
            status,
            response.IsSuccess,
            response.StatusCode is null ? null : (int)response.StatusCode.Value,
            response.Kind.ToString(),
            durationMilliseconds,
            error,
            response.ResponsePreview,
            response.ResponseTruncated,
            cancellationToken);
    }

    private async Task<MonitorCheck> RecordSyntheticFailureAsync(UptimeMonitor monitor, string error, CancellationToken cancellationToken)
    {
        return await RecordAsync(
            monitor,
            MonitorStatus.Down,
            succeeded: false,
            statusCode: null,
            resultKind: SafeOutboundResultKind.InvalidRequest.ToString(),
            durationMilliseconds: 0,
            error,
            responsePreview: string.Empty,
            responseTruncated: false,
            cancellationToken);
    }

    private async Task<MonitorCheck> RecordAsync(
        UptimeMonitor monitor,
        MonitorStatus status,
        bool succeeded,
        int? statusCode,
        string resultKind,
        long durationMilliseconds,
        string? error,
        string? responsePreview,
        bool responseTruncated,
        CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var previousStatus = monitor.CurrentStatus;
        monitor.RecordCheck(status, now);
        var check = new MonitorCheck(
            monitor,
            status,
            succeeded,
            statusCode,
            resultKind,
            durationMilliseconds,
            error,
            responsePreview,
            responseTruncated,
            now);
        dbContext.MonitorChecks.Add(check);
        await incidentAutomationService.HandleMonitorResultAsync(monitor, check, previousStatus, now, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        return check;
    }

    public static MonitorStatus Classify(SafeOutboundResponse response, long durationMilliseconds, int slowThresholdMilliseconds)
    {
        if (!response.IsSuccess)
        {
            return MonitorStatus.Down;
        }

        return durationMilliseconds > slowThresholdMilliseconds ? MonitorStatus.Slow : MonitorStatus.Up;
    }
}

public sealed record MonitorCheckBatchResult(int Processed, int BatchSize);
