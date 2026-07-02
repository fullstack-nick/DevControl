using System.Diagnostics;
using DevControl.Api.Endpoints;
using DevControl.Api.GitHub;
using DevControl.Api.Monitoring;
using DevControl.Api.Observability;
using DevControl.Api.Security;
using DevControl.Api.Webhooks;
using DevControl.Application.Health;
using DevControl.Infrastructure.Database;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Prometheus;
using Prometheus.DotNetRuntime;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration.AddEnvironmentVariables(prefix: "DEVCONTROL_");
var metricsAccess = MetricsAccessOptions.FromConfiguration(builder.Configuration, builder.Environment);

builder.Logging.ClearProviders();
builder.Logging.AddJsonConsole(options =>
{
    options.IncludeScopes = true;
    options.TimestampFormat = "O";
});

builder.Services.AddDevControlInfrastructure(builder.Configuration);
builder.Services.AddDataProtection()
    .PersistKeysToDbContext<DevControlDbContext>()
    .SetApplicationName("DevControl");
builder.Services.AddDevControlSecurity(builder.Configuration, builder.Environment);
builder.Services.AddScoped<WebhookSecretService>();
builder.Services.AddScoped<WebhookEventPublisher>();
builder.Services.AddScoped<WebhookDeliveryService>();
builder.Services.AddScoped<MonitorProvisioningService>();
builder.Services.AddScoped<IncidentAutomationService>();
builder.Services.AddScoped<MonitorCheckService>();
builder.Services.AddSingleton(GitHubAppOptions.FromConfiguration(builder.Configuration));
builder.Services.AddHttpClient<IGitHubAppClient, GitHubAppClient>();
builder.Services.AddHttpClient<IGitHubOidcTokenValidator, GitHubOidcTokenValidator>();
builder.Services.AddScoped<GitHubSyncService>();
builder.Services.AddScoped<SchedulerTickService>();
builder.Services.AddScoped<RetentionCleanupService>();
builder.Services.AddSingleton(RetentionCleanupOptions.FromConfiguration(builder.Configuration));
builder.Services.AddSingleton(ObservabilityProxyOptions.FromConfiguration(builder.Configuration));
builder.Services.AddHttpClient<CloudRunIdentityTokenProvider>(client =>
{
    client.BaseAddress = new Uri("http://metadata.google.internal/");
    client.Timeout = TimeSpan.FromSeconds(10);
});
builder.Services.AddHttpClient<ObservabilityProxyService>(client =>
{
    client.Timeout = Timeout.InfiniteTimeSpan;
});

var app = builder.Build();
var runtimeMetrics = metricsAccess.Enabled
    ? DotNetRuntimeStatsBuilder.Default().StartCollecting()
    : null;
if (runtimeMetrics is not null)
{
    app.Lifetime.ApplicationStopping.Register(runtimeMetrics.Dispose);
}

app.UseForwardedHeaders();

app.Use(async (context, next) =>
{
    var logger = context.RequestServices
        .GetRequiredService<ILoggerFactory>()
        .CreateLogger("DevControl.Http");

    var startedAt = Stopwatch.GetTimestamp();
    var shouldLog = ShouldLogAccess(context.Request.Path);

    using var scope = shouldLog ? logger.BeginScope(new Dictionary<string, object?>
    {
        ["TraceId"] = context.TraceIdentifier,
        ["Method"] = context.Request.Method,
        ["Path"] = context.Request.Path.Value
    }) : null;

    try
    {
        await next();
    }
    finally
    {
        var elapsed = Stopwatch.GetElapsedTime(startedAt);
        DevControlMetrics.RecordHttpRequest(
            context.Request.Method,
            context.Request.Path.Value,
            context.Response.StatusCode,
            elapsed);

        if (shouldLog)
        {
            logger.LogInformation(
                "HTTP {Method} {Path} responded {StatusCode} in {ElapsedMilliseconds} ms",
                context.Request.Method,
                context.Request.Path.Value,
                context.Response.StatusCode,
                elapsed.TotalMilliseconds);
        }
    }
});

if (metricsAccess.Enabled)
{
    app.Use(async (context, next) =>
    {
        if (context.Request.Path == "/metrics" &&
            !metricsAccess.IsAuthorized(context.Request.Headers.Authorization.ToString()))
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }

        await next();
    });

    app.UseMetricServer();
}

if (IsStartupMigrationEnabled())
{
    await using var scope = app.Services.CreateAsyncScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<DevControlDbContext>();
    await dbContext.Database.MigrateAsync();
}

app.UseDefaultFiles();
app.UseStaticFiles();
app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

app.MapGet("/health/live", () => Results.Ok(HealthPayload.Live()));
if (!metricsAccess.Enabled)
{
    app.MapGet("/metrics", () => Results.NotFound());
}

app.MapGet("/health/ready", async (
    DevControlDbContext dbContext,
    ILoggerFactory loggerFactory,
    CancellationToken cancellationToken) =>
{
    var logger = loggerFactory.CreateLogger("DevControl.Health");

    try
    {
        var canConnect = await dbContext.Database.CanConnectAsync(cancellationToken);
        if (canConnect)
        {
            return Results.Ok(HealthPayload.Ready());
        }

        logger.LogWarning("PostgreSQL readiness check returned false.");
        return Results.Json(HealthPayload.NotReady(), statusCode: StatusCodes.Status503ServiceUnavailable);
    }
    catch (Exception exception)
    {
        logger.LogWarning(exception, "PostgreSQL readiness check failed.");
        return Results.Json(HealthPayload.NotReady(), statusCode: StatusCodes.Status503ServiceUnavailable);
    }
});

app.MapAuthEndpoints();
app.MapTenantEndpoints();
app.MapAppRegistryEndpoints();
app.MapApiKeyEndpoints();
app.MapFeatureFlagEndpoints();
app.MapWebhookEndpoints();
app.MapMonitoringEndpoints();
app.MapGitHubEndpoints();
app.MapOperatorEndpoints();
app.MapPublicConfigEndpoints();
app.MapObservabilityProxyEndpoints();

app.MapFallbackToFile("index.html");

await app.RunAsync();

static bool IsStartupMigrationEnabled()
{
    var rawValue = Environment.GetEnvironmentVariable("DEVCONTROL_RUN_MIGRATIONS_ON_STARTUP");
    return bool.TryParse(rawValue, out var enabled) && enabled;
}

static bool ShouldLogAccess(PathString path)
{
    var value = path.Value ?? string.Empty;
    if (value is "/health/live" or "/health/ready" or "/metrics")
    {
        return false;
    }

    return !value.StartsWith("/assets/", StringComparison.OrdinalIgnoreCase) &&
        !value.EndsWith(".js", StringComparison.OrdinalIgnoreCase) &&
        !value.EndsWith(".css", StringComparison.OrdinalIgnoreCase) &&
        !value.EndsWith(".ico", StringComparison.OrdinalIgnoreCase) &&
        !value.EndsWith(".png", StringComparison.OrdinalIgnoreCase) &&
        !value.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) &&
        !value.EndsWith(".svg", StringComparison.OrdinalIgnoreCase) &&
        !value.EndsWith(".webp", StringComparison.OrdinalIgnoreCase);
}

public partial class Program
{
}
