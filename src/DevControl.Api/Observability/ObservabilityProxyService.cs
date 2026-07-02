using System.Net.Http.Headers;
using DevControl.Application.Security;
using DevControl.Api.Security;
using DevControl.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;

namespace DevControl.Api.Observability;

public sealed class ObservabilityProxyService(
    HttpClient httpClient,
    ObservabilityProxyOptions options,
    CloudRunIdentityTokenProvider identityTokenProvider,
    CurrentUserAccessor currentUserAccessor,
    DevControlDbContext dbContext,
    ILogger<ObservabilityProxyService> logger)
{
    private static readonly HashSet<string> HopByHopHeaders = new(StringComparer.OrdinalIgnoreCase)
    {
        "Connection",
        "Keep-Alive",
        "Proxy-Authenticate",
        "Proxy-Authorization",
        "TE",
        "Trailer",
        "Transfer-Encoding",
        "Upgrade"
    };

    private static readonly HashSet<string> BlockedRequestHeaders = new(HopByHopHeaders, StringComparer.OrdinalIgnoreCase)
    {
        "Authorization",
        "Cookie",
        "Host",
        "X-CSRF-TOKEN",
        "X-WEBAUTH-USER",
        "X-WEBAUTH-EMAIL",
        "X-WEBAUTH-NAME",
        "X-WEBAUTH-ROLE"
    };

    public async Task ProxyAsync(HttpContext httpContext)
    {
        var upstream = options.ResolveUpstream(httpContext);
        if (upstream is null)
        {
            httpContext.Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }

        var actor = await currentUserAccessor.GetOrCreateAsync(httpContext.RequestAborted);
        var role = await dbContext.OrganizationMembers
            .Where(member => member.UserId == actor.Id && member.IsActive)
            .OrderByDescending(member => member.Role)
            .Select(member => (DevControl.Domain.Enums.OrganizationRole?)member.Role)
            .FirstOrDefaultAsync(httpContext.RequestAborted);

        if (role is null || !RolePermissions.AtLeast(role.Value, options.RequiredRole))
        {
            httpContext.Response.StatusCode = StatusCodes.Status403Forbidden;
            return;
        }

        using var upstreamRequest = await BuildUpstreamRequestAsync(httpContext, upstream, actor, role.Value);
        using var upstreamResponse = await httpClient.SendAsync(
            upstreamRequest,
            HttpCompletionOption.ResponseHeadersRead,
            httpContext.RequestAborted);

        await CopyResponseAsync(httpContext, upstream, upstreamResponse);
    }

    private async Task<HttpRequestMessage> BuildUpstreamRequestAsync(
        HttpContext httpContext,
        Uri upstream,
        CurrentUser actor,
        DevControl.Domain.Enums.OrganizationRole role)
    {
        var targetUri = BuildTargetUri(httpContext, upstream);
        var request = new HttpRequestMessage(new HttpMethod(httpContext.Request.Method), targetUri);

        foreach (var header in httpContext.Request.Headers)
        {
            if (BlockedRequestHeaders.Contains(header.Key))
            {
                continue;
            }

            request.Headers.TryAddWithoutValidation(header.Key, header.Value.ToArray());
        }

        if (HasBody(httpContext.Request))
        {
            request.Content = new StreamContent(httpContext.Request.Body);
            foreach (var header in httpContext.Request.Headers)
            {
                if (!BlockedRequestHeaders.Contains(header.Key) &&
                    !request.Headers.Contains(header.Key))
                {
                    request.Content.Headers.TryAddWithoutValidation(header.Key, header.Value.ToArray());
                }
            }
        }

        request.Headers.TryAddWithoutValidation("X-WEBAUTH-USER", actor.NormalizedEmail);
        request.Headers.TryAddWithoutValidation("X-WEBAUTH-EMAIL", actor.Email);
        request.Headers.TryAddWithoutValidation("X-WEBAUTH-NAME", actor.DisplayName);
        request.Headers.TryAddWithoutValidation("X-WEBAUTH-ROLE", role.ToString());
        request.Headers.TryAddWithoutValidation("X-Forwarded-Host", httpContext.Request.Host.Value);
        request.Headers.TryAddWithoutValidation("X-Forwarded-Proto", httpContext.Request.Scheme);
        request.Headers.TryAddWithoutValidation("X-Forwarded-Prefix", ObservabilityProxyOptions.PathPrefix);

        if (options.ShouldUseIdentityToken(upstream))
        {
            var token = await identityTokenProvider.GetTokenAsync(upstream, httpContext.RequestAborted);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        return request;
    }

    private static Uri BuildTargetUri(HttpContext httpContext, Uri upstream)
    {
        var path = httpContext.Request.Path.ToUriComponent();
        var query = httpContext.Request.QueryString.ToUriComponent();
        return new Uri(upstream, $"{path}{query}");
    }

    private async Task CopyResponseAsync(HttpContext httpContext, Uri upstream, HttpResponseMessage upstreamResponse)
    {
        httpContext.Response.StatusCode = (int)upstreamResponse.StatusCode;

        foreach (var header in upstreamResponse.Headers)
        {
            CopyResponseHeader(httpContext, upstream, header.Key, header.Value);
        }

        foreach (var header in upstreamResponse.Content.Headers)
        {
            CopyResponseHeader(httpContext, upstream, header.Key, header.Value);
        }

        httpContext.Response.Headers.Remove("transfer-encoding");

        if (HttpMethods.IsHead(httpContext.Request.Method))
        {
            return;
        }

        try
        {
            await upstreamResponse.Content.CopyToAsync(httpContext.Response.Body, httpContext.RequestAborted);
        }
        catch (OperationCanceledException) when (httpContext.RequestAborted.IsCancellationRequested)
        {
            logger.LogDebug("Observability proxy response copy was canceled.");
        }
    }

    private static void CopyResponseHeader(
        HttpContext httpContext,
        Uri upstream,
        string name,
        IEnumerable<string> values)
    {
        if (HopByHopHeaders.Contains(name))
        {
            return;
        }

        var copiedValues = values.ToArray();
        if (name.Equals("Location", StringComparison.OrdinalIgnoreCase))
        {
            copiedValues = copiedValues
                .Select(value => RewriteLocation(upstream, value))
                .ToArray();
        }

        httpContext.Response.Headers[name] = copiedValues;
    }

    private static string RewriteLocation(Uri upstream, string value)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var location) ||
            !string.Equals(location.Scheme, upstream.Scheme, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(location.Host, upstream.Host, StringComparison.OrdinalIgnoreCase))
        {
            return value;
        }

        return $"{location.PathAndQuery}{location.Fragment}";
    }

    private static bool HasBody(HttpRequest request)
    {
        return request.ContentLength > 0 ||
            request.Headers.ContainsKey("Transfer-Encoding") ||
            HttpMethods.IsPost(request.Method) ||
            HttpMethods.IsPut(request.Method) ||
            HttpMethods.IsPatch(request.Method);
    }
}
