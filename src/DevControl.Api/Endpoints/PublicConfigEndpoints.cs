using DevControl.Api.Observability;

namespace DevControl.Api.Endpoints;

public static class PublicConfigEndpoints
{
    public static void MapPublicConfigEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/public/config", (HttpContext httpContext, ObservabilityProxyOptions options) =>
            Results.Ok(new PublicConfigResponse(options.ResolvePublicPath(httpContext))));
    }

    private sealed record PublicConfigResponse(string? ObservabilityUrl);
}
