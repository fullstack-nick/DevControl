using System.Net;
using System.Net.Http;

namespace DevControl.Application.Outbound;

public interface ISafeOutboundHttpClient
{
    Task<SafeOutboundResponse> SendAsync(SafeOutboundRequest request, CancellationToken cancellationToken);
}

public sealed record SafeOutboundRequest(
    Uri Url,
    HttpMethod Method,
    IReadOnlyDictionary<string, string> Headers,
    string? Body,
    string ContentType,
    OutboundRequestPolicy Policy);

public sealed record OutboundRequestPolicy(
    bool RequireHttps,
    IReadOnlySet<int> AllowedPorts,
    TimeSpan Timeout,
    int MaxPreviewBytes,
    int MaxResponseBytes,
    int MaxRedirects)
{
    public static OutboundRequestPolicy Webhook { get; } = new(
        RequireHttps: true,
        AllowedPorts: new HashSet<int> { 443 },
        Timeout: TimeSpan.FromSeconds(10),
        MaxPreviewBytes: 16 * 1024,
        MaxResponseBytes: 64 * 1024,
        MaxRedirects: 0);

    public static OutboundRequestPolicy Monitor { get; } = new(
        RequireHttps: false,
        AllowedPorts: new HashSet<int> { 80, 443 },
        Timeout: TimeSpan.FromSeconds(5),
        MaxPreviewBytes: 4 * 1024,
        MaxResponseBytes: 64 * 1024,
        MaxRedirects: 2);
}

public sealed record SafeOutboundResponse(
    SafeOutboundResultKind Kind,
    HttpStatusCode? StatusCode,
    string ResponsePreview,
    bool ResponseTruncated,
    long ResponseBytesRead,
    string? Error,
    TimeSpan Duration,
    string? RedirectLocation = null)
{
    public bool IsSuccess => Kind == SafeOutboundResultKind.Completed &&
        StatusCode is >= HttpStatusCode.OK and < HttpStatusCode.MultipleChoices;
}

public enum SafeOutboundResultKind
{
    Completed = 0,
    Blocked = 1,
    Timeout = 2,
    NetworkError = 3,
    InvalidRequest = 4
}
