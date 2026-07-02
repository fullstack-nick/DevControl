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
        app.MapMethods(
                $"{ObservabilityProxyOptions.PathPrefix}/{{**path}}",
                Methods,
                ProxyOrRedirectToSignInAsync);
    }

    private static async Task ProxyOrRedirectToSignInAsync(HttpContext httpContext, ObservabilityProxyService proxy)
    {
        if (httpContext.User.Identity?.IsAuthenticated != true)
        {
            RedirectToAppSignIn(httpContext);
            return;
        }

        await proxy.ProxyAsync(httpContext);
    }

    private static void RedirectToAppSignIn(HttpContext httpContext)
    {
        var returnUrl = Uri.EscapeDataString(
            httpContext.Request.PathBase +
            httpContext.Request.Path +
            httpContext.Request.QueryString);
        httpContext.Response.Redirect($"/?returnUrl={returnUrl}");
    }
}
