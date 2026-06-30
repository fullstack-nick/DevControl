using System.Diagnostics;
using DevControl.Api.Endpoints;
using DevControl.Api.Security;
using DevControl.Application.Health;
using DevControl.Infrastructure.Database;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration.AddEnvironmentVariables(prefix: "DEVCONTROL_");

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

var app = builder.Build();

app.UseForwardedHeaders();

app.Use(async (context, next) =>
{
    var logger = context.RequestServices
        .GetRequiredService<ILoggerFactory>()
        .CreateLogger("DevControl.Http");

    var startedAt = Stopwatch.GetTimestamp();

    using var scope = logger.BeginScope(new Dictionary<string, object?>
    {
        ["TraceId"] = context.TraceIdentifier,
        ["Method"] = context.Request.Method,
        ["Path"] = context.Request.Path.Value
    });

    try
    {
        await next();
    }
    finally
    {
        var elapsed = Stopwatch.GetElapsedTime(startedAt);
        logger.LogInformation(
            "HTTP {Method} {Path} responded {StatusCode} in {ElapsedMilliseconds} ms",
            context.Request.Method,
            context.Request.Path.Value,
            context.Response.StatusCode,
            elapsed.TotalMilliseconds);
    }
});

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

app.MapFallbackToFile("index.html");

await app.RunAsync();

static bool IsStartupMigrationEnabled()
{
    var rawValue = Environment.GetEnvironmentVariable("DEVCONTROL_RUN_MIGRATIONS_ON_STARTUP");
    return bool.TryParse(rawValue, out var enabled) && enabled;
}

public partial class Program
{
}
