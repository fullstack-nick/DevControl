using System.Buffers;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using DevControl.Application.Outbound;
using Microsoft.Extensions.Logging;

namespace DevControl.Infrastructure.Outbound;

public sealed class SafeOutboundHttpClient(
    OutboundRequestGuard guard,
    ILogger<SafeOutboundHttpClient> logger) : ISafeOutboundHttpClient
{
    public async Task<SafeOutboundResponse> SendAsync(SafeOutboundRequest request, CancellationToken cancellationToken)
    {
        var startedAt = Stopwatch.GetTimestamp();
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(request.Policy.Timeout);
        var current = request.Url;

        try
        {
            for (var redirectCount = 0; redirectCount <= request.Policy.MaxRedirects; redirectCount++)
            {
                var response = await SendOnceAsync(request with { Url = current }, timeout.Token, startedAt);
                if (!IsRedirect(response.StatusCode))
                {
                    return response;
                }

                if (redirectCount == request.Policy.MaxRedirects)
                {
                    return new SafeOutboundResponse(
                        SafeOutboundResultKind.InvalidRequest,
                        response.StatusCode,
                        response.ResponsePreview,
                        response.ResponseTruncated,
                        response.ResponseBytesRead,
                        "Outbound request exceeded the redirect limit.",
                        Stopwatch.GetElapsedTime(startedAt));
                }

                if (!TryResolveRedirect(current, response.RedirectLocation, out var redirected, out var redirectError))
                {
                    return new SafeOutboundResponse(
                        SafeOutboundResultKind.InvalidRequest,
                        response.StatusCode,
                        response.ResponsePreview,
                        response.ResponseTruncated,
                        response.ResponseBytesRead,
                        redirectError ?? "Outbound redirect location is invalid.",
                        Stopwatch.GetElapsedTime(startedAt));
                }

                current = redirected!;
            }

            return new SafeOutboundResponse(
                SafeOutboundResultKind.InvalidRequest,
                null,
                string.Empty,
                ResponseTruncated: false,
                ResponseBytesRead: 0,
                "Outbound request exceeded the redirect limit.",
                Stopwatch.GetElapsedTime(startedAt));
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new SafeOutboundResponse(
                SafeOutboundResultKind.Timeout,
                null,
                string.Empty,
                ResponseTruncated: false,
                ResponseBytesRead: 0,
                $"Outbound request timed out after {request.Policy.Timeout.TotalSeconds:0.#} seconds.",
                Stopwatch.GetElapsedTime(startedAt));
        }
        catch (Exception exception) when (exception is HttpRequestException or IOException or SocketException)
        {
            logger.LogInformation(exception, "Safe outbound request to {Host} failed.", current.Host);
            return new SafeOutboundResponse(
                SafeOutboundResultKind.NetworkError,
                null,
                string.Empty,
                ResponseTruncated: false,
                ResponseBytesRead: 0,
                "Outbound request failed before a response was received.",
                Stopwatch.GetElapsedTime(startedAt));
        }
    }

    private async Task<SafeOutboundResponse> SendOnceAsync(
        SafeOutboundRequest request,
        CancellationToken cancellationToken,
        long startedAt)
    {
        var guardResult = await guard.ValidateAsync(request.Url, request.Policy, cancellationToken);
        if (!guardResult.IsAllowed || guardResult.Address is null)
        {
            return new SafeOutboundResponse(
                SafeOutboundResultKind.Blocked,
                null,
                string.Empty,
                ResponseTruncated: false,
                ResponseBytesRead: 0,
                guardResult.Error ?? "Outbound request blocked.",
                Stopwatch.GetElapsedTime(startedAt));
        }

        using var handler = CreateHandler(guardResult.Address, guardResult.Port);
        using var httpClient = new HttpClient(handler)
        {
            Timeout = Timeout.InfiniteTimeSpan
        };
        using var httpRequest = new HttpRequestMessage(request.Method, request.Url);

        foreach (var header in request.Headers)
        {
            httpRequest.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        if (request.Body is not null)
        {
            httpRequest.Content = new StringContent(request.Body, Encoding.UTF8, request.ContentType);
        }

        using var response = await httpClient.SendAsync(
            httpRequest,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);
        var preview = await ReadPreviewAsync(response.Content, request.Policy, cancellationToken);

        return new SafeOutboundResponse(
            SafeOutboundResultKind.Completed,
            response.StatusCode,
            preview.Text,
            preview.Truncated,
            preview.BytesRead,
            null,
            Stopwatch.GetElapsedTime(startedAt),
            response.Headers.Location?.ToString());
    }

    private static bool IsRedirect(HttpStatusCode? statusCode)
    {
        return statusCode is HttpStatusCode.Moved or
            HttpStatusCode.Redirect or
            HttpStatusCode.RedirectMethod or
            HttpStatusCode.TemporaryRedirect or
            HttpStatusCode.PermanentRedirect;
    }

    private static bool TryResolveRedirect(Uri current, string? location, out Uri? redirected, out string? error)
    {
        redirected = null;
        error = null;

        if (string.IsNullOrWhiteSpace(location))
        {
            error = "Outbound redirect did not include a Location header.";
            return false;
        }

        if (!Uri.TryCreate(current, location, out redirected) || !redirected.IsAbsoluteUri)
        {
            error = "Outbound redirect location must be absolute or relative to the current URL.";
            return false;
        }

        return true;
    }

    private static SocketsHttpHandler CreateHandler(IPAddress address, int port)
    {
        return new SocketsHttpHandler
        {
            AllowAutoRedirect = false,
            ConnectCallback = async (_, cancellationToken) =>
            {
                var socket = new Socket(address.AddressFamily, SocketType.Stream, ProtocolType.Tcp)
                {
                    NoDelay = true
                };

                try
                {
                    await socket.ConnectAsync(new IPEndPoint(address, port), cancellationToken);
                    return new NetworkStream(socket, ownsSocket: true);
                }
                catch
                {
                    socket.Dispose();
                    throw;
                }
            }
        };
    }

    private static async Task<ResponsePreview> ReadPreviewAsync(
        HttpContent content,
        OutboundRequestPolicy policy,
        CancellationToken cancellationToken)
    {
        await using var stream = await content.ReadAsStreamAsync(cancellationToken);
        using var preview = new MemoryStream(capacity: Math.Min(policy.MaxPreviewBytes, 16 * 1024));
        var buffer = ArrayPool<byte>.Shared.Rent(8192);
        long totalRead = 0;
        var truncated = false;

        try
        {
            while (totalRead < policy.MaxResponseBytes)
            {
                var remaining = policy.MaxResponseBytes - totalRead;
                var readSize = (int)Math.Min(buffer.Length, remaining);
                var read = await stream.ReadAsync(buffer.AsMemory(0, readSize), cancellationToken);
                if (read == 0)
                {
                    break;
                }

                totalRead += read;
                var previewRemaining = policy.MaxPreviewBytes - preview.Length;
                if (previewRemaining > 0)
                {
                    preview.Write(buffer, 0, (int)Math.Min(read, previewRemaining));
                }
            }

            if (totalRead >= policy.MaxResponseBytes)
            {
                truncated = true;
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }

        var text = Encoding.UTF8.GetString(preview.ToArray());
        return new ResponsePreview(text, truncated || preview.Length >= policy.MaxPreviewBytes, totalRead);
    }

    private sealed record ResponsePreview(string Text, bool Truncated, long BytesRead);
}
