using Microsoft.AspNetCore.Antiforgery;

namespace DevControl.Api.Security;

public sealed class CsrfEndpointFilter : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var antiforgery = context.HttpContext.RequestServices.GetRequiredService<IAntiforgery>();
        try
        {
            await antiforgery.ValidateRequestAsync(context.HttpContext);
        }
        catch (AntiforgeryValidationException)
        {
            return Results.Problem("Invalid CSRF token.", statusCode: StatusCodes.Status400BadRequest);
        }

        return await next(context);
    }
}

public static class CsrfEndpointFilterExtensions
{
    public static RouteHandlerBuilder RequireCsrf(this RouteHandlerBuilder builder)
    {
        return builder.AddEndpointFilter<CsrfEndpointFilter>();
    }
}
