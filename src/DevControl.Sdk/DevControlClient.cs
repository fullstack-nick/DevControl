using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace DevControl.Sdk;

public sealed class DevControlClient : IDisposable
{
    private const string SnapshotPath = "/api/runtime/flags/snapshot";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient httpClient;
    private readonly bool ownsHttpClient;
    private readonly DevControlClientOptions options;
    private readonly object sync = new();
    private DevControlFlagSnapshot? snapshot;
    private DateTimeOffset? lastRefreshAttemptAt;
    private DateTimeOffset? lastKillSwitchRefreshAttemptAt;
    private bool disposed;

    public DevControlClient(Uri baseAddress, string apiKey, HttpClient? httpClient = null, DevControlClientOptions? options = null)
    {
        BaseAddress = baseAddress ?? throw new ArgumentNullException(nameof(baseAddress));
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new ArgumentException("API key is required.", nameof(apiKey));
        }

        ApiKey = apiKey.Trim();
        this.options = options ?? new DevControlClientOptions();
        this.httpClient = httpClient ?? new HttpClient();
        ownsHttpClient = httpClient is null;
    }

    public Uri BaseAddress { get; }

    public string ApiKey { get; }

    public long RefreshAttemptCount { get; private set; }

    public long SuccessfulRefreshCount { get; private set; }

    public string? SnapshotVersion
    {
        get
        {
            lock (sync)
            {
                return snapshot?.Version;
            }
        }
    }

    public bool IsEnabled(string key, bool defaultValue = false)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return defaultValue;
        }

        lock (sync)
        {
            return snapshot is not null && snapshot.Flags.TryGetValue(key.Trim(), out var enabled)
                ? enabled
                : defaultValue;
        }
    }

    public bool IsKilled(string key, bool defaultValue = true)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return defaultValue;
        }

        lock (sync)
        {
            return snapshot is not null && snapshot.KillSwitches.TryGetValue(key.Trim(), out var killed)
                ? killed
                : defaultValue;
        }
    }

    public async Task<DevControlRefreshResult> RefreshIfStaleAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        lock (sync)
        {
            if (snapshot is not null &&
                lastRefreshAttemptAt is not null &&
                now - lastRefreshAttemptAt.Value < options.RefreshInterval)
            {
                return DevControlRefreshResult.Skipped(snapshot.Version);
            }
        }

        return await RefreshAsync(cancellationToken);
    }

    public async Task<DevControlRefreshResult> RefreshKillSwitchesIfStaleAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        lock (sync)
        {
            if (snapshot is not null &&
                lastKillSwitchRefreshAttemptAt is not null &&
                now - lastKillSwitchRefreshAttemptAt.Value < options.KillSwitchRefreshInterval)
            {
                return DevControlRefreshResult.Skipped(snapshot.Version);
            }

            lastKillSwitchRefreshAttemptAt = now;
        }

        return await RefreshAsync(cancellationToken);
    }

    public async Task RunBackgroundRefreshAsync(CancellationToken cancellationToken)
    {
        await RefreshAsync(cancellationToken);
        using var timer = new PeriodicTimer(options.RefreshInterval);
        while (await timer.WaitForNextTickAsync(cancellationToken))
        {
            await RefreshAsync(cancellationToken);
        }
    }

    public async Task<DevControlRefreshResult> RefreshAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        DevControlFlagSnapshot? current;
        lock (sync)
        {
            RefreshAttemptCount++;
            lastRefreshAttemptAt = DateTimeOffset.UtcNow;
            current = snapshot;
        }

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(options.RequestTimeout);
        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(BaseAddress, SnapshotPath));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", ApiKey);
        if (!string.IsNullOrWhiteSpace(current?.ETag))
        {
            request.Headers.TryAddWithoutValidation("If-None-Match", current.ETag);
        }

        try
        {
            using var response = await httpClient.SendAsync(request, timeout.Token);
            if (response.StatusCode == HttpStatusCode.NotModified && current is not null)
            {
                lock (sync)
                {
                    SuccessfulRefreshCount++;
                }

                return DevControlRefreshResult.NotModified(current.Version);
            }

            if (!response.IsSuccessStatusCode)
            {
                return DevControlRefreshResult.Failed(current?.Version, $"Snapshot request failed with {(int)response.StatusCode}.");
            }

            var payload = await response.Content.ReadFromJsonAsync<SnapshotPayload>(JsonOptions, timeout.Token);
            if (payload is null || string.IsNullOrWhiteSpace(payload.Version))
            {
                return DevControlRefreshResult.Failed(current?.Version, "Snapshot response was empty or invalid.");
            }

            var next = new DevControlFlagSnapshot(
                payload.Version,
                response.Headers.ETag?.ToString() ?? QuoteETag(payload.Version),
                payload.Flags ?? new Dictionary<string, bool>(StringComparer.Ordinal),
                payload.KillSwitches ?? new Dictionary<string, bool>(StringComparer.Ordinal),
                DateTimeOffset.UtcNow);

            lock (sync)
            {
                snapshot = next;
                SuccessfulRefreshCount++;
            }

            return DevControlRefreshResult.Updated(next.Version);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return DevControlRefreshResult.Failed(current?.Version, "Snapshot request timed out.");
        }
        catch (HttpRequestException exception)
        {
            return DevControlRefreshResult.Failed(current?.Version, exception.Message);
        }
        catch (JsonException exception)
        {
            return DevControlRefreshResult.Failed(current?.Version, exception.Message);
        }
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        if (ownsHttpClient)
        {
            httpClient.Dispose();
        }

        disposed = true;
    }

    private static string QuoteETag(string version)
    {
        return $"\"{version}\"";
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
    }

    private sealed record SnapshotPayload(
        string Version,
        DateTimeOffset GeneratedAt,
        int RefreshIntervalSeconds,
        int KillSwitchRefreshIntervalSeconds,
        Dictionary<string, bool>? Flags,
        Dictionary<string, bool>? KillSwitches);
}
