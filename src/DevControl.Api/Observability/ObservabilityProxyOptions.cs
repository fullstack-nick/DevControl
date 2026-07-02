using DevControl.Domain.Enums;

namespace DevControl.Api.Observability;

public sealed class ObservabilityProxyOptions
{
    public const string PathPrefix = "/observability";

    private ObservabilityProxyOptions(
        Uri? upstreamUrl,
        bool? requiresIdentityToken,
        OrganizationRole requiredRole)
    {
        UpstreamUrl = upstreamUrl;
        RequiresIdentityToken = requiresIdentityToken;
        RequiredRole = requiredRole;
    }

    public Uri? UpstreamUrl { get; }

    public bool? RequiresIdentityToken { get; }

    public OrganizationRole RequiredRole { get; }

    public static ObservabilityProxyOptions FromConfiguration(IConfiguration configuration)
    {
        var upstreamUrl = TryCreateUri(configuration["OBSERVABILITY_UPSTREAM_URL"]);
        var requiresIdentityToken = bool.TryParse(configuration["OBSERVABILITY_PROXY_REQUIRES_ID_TOKEN"], out var configured)
            ? configured
            : (bool?)null;
        var requiredRole = Enum.TryParse<OrganizationRole>(
            configuration["OBSERVABILITY_REQUIRED_ROLE"],
            ignoreCase: true,
            out var configuredRole)
            ? configuredRole
            : OrganizationRole.Viewer;

        return new ObservabilityProxyOptions(upstreamUrl, requiresIdentityToken, requiredRole);
    }

    public Uri? ResolveUpstream(HttpContext httpContext)
    {
        if (UpstreamUrl is not null)
        {
            return UpstreamUrl;
        }

        var host = httpContext.Request.Host.Host;
        if (!host.EndsWith(".a.run.app", StringComparison.OrdinalIgnoreCase) ||
            !host.StartsWith("devcontrol-", StringComparison.OrdinalIgnoreCase) ||
            host.StartsWith("devcontrol-observability-", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return new Uri($"https://devcontrol-observability-{host["devcontrol-".Length..]}");
    }

    public bool ShouldUseIdentityToken(Uri upstream)
    {
        return RequiresIdentityToken ?? upstream.Host.EndsWith(".a.run.app", StringComparison.OrdinalIgnoreCase);
    }

    public string? ResolvePublicPath(HttpContext httpContext)
    {
        return ResolveUpstream(httpContext) is null ? null : $"{PathPrefix}/";
    }

    private static Uri? TryCreateUri(string? rawValue)
    {
        return Uri.TryCreate(rawValue, UriKind.Absolute, out var uri) &&
            uri.Scheme is "https" or "http"
                ? uri
                : null;
    }
}
