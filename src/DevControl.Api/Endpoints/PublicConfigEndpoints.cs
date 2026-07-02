namespace DevControl.Api.Endpoints;

public static class PublicConfigEndpoints
{
    public static void MapPublicConfigEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/public/config", (HttpContext httpContext, IConfiguration configuration) =>
            Results.Ok(new PublicConfigResponse(ResolveObservabilityUrl(httpContext, configuration))));
    }

    private static string? ResolveObservabilityUrl(HttpContext httpContext, IConfiguration configuration)
    {
        var configuredUrl = configuration["OBSERVABILITY_URL"];
        if (Uri.TryCreate(configuredUrl, UriKind.Absolute, out var configuredUri) &&
            configuredUri.Scheme is "https" or "http")
        {
            return configuredUri.ToString().TrimEnd('/');
        }

        var host = httpContext.Request.Host.Host;
        if (!host.EndsWith(".a.run.app", StringComparison.OrdinalIgnoreCase) ||
            !host.StartsWith("devcontrol-", StringComparison.OrdinalIgnoreCase) ||
            host.StartsWith("devcontrol-observability-", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return $"https://devcontrol-observability-{host["devcontrol-".Length..]}";
    }

    private sealed record PublicConfigResponse(string? ObservabilityUrl);
}
