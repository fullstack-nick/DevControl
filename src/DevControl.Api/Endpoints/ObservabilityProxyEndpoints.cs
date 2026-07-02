using DevControl.Api.Observability;

namespace DevControl.Api.Endpoints;

public static class ObservabilityProxyEndpoints
{
    private static readonly string[] Methods =
    [
        HttpMethods.Get,
        HttpMethods.Post,
        HttpMethods.Put,
        HttpMethods.Patch,
        HttpMethods.Delete,
        HttpMethods.Head,
        HttpMethods.Options
    ];

    public static void MapObservabilityProxyEndpoints(this WebApplication app)
    {
        app.MapGet(ObservabilityProxyOptions.PathPrefix, () =>
                Results.Redirect($"{ObservabilityProxyOptions.PathPrefix}/"))
            .RequireAuthorization();

        app.MapMethods(
                $"{ObservabilityProxyOptions.PathPrefix}/{{**path}}",
                Methods,
                async (HttpContext httpContext, ObservabilityProxyService proxy) =>
                    await proxy.ProxyAsync(httpContext))
            .RequireAuthorization();
    }
}
