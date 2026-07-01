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

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(request.Policy.Timeout);

        try
        {
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
                timeout.Token);
            var preview = await ReadPreviewAsync(response.Content, request.Policy, timeout.Token);

            return new SafeOutboundResponse(
                SafeOutboundResultKind.Completed,
                response.StatusCode,
                preview.Text,
                preview.Truncated,
                preview.BytesRead,
                null,
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
            logger.LogInformation(exception, "Safe outbound request to {Host} failed.", request.Url.Host);
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
