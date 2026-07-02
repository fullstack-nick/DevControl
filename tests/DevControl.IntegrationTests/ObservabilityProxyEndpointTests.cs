using System.Net;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Text;
using DevControl.Infrastructure.Database;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace DevControl.IntegrationTests;

[Collection(PostgresIntegrationCollection.Name)]
public sealed class ObservabilityProxyEndpointTests
{
    [Fact]
    public async Task ObservabilityProxy_ForwardsAuthenticatedOrgMemberWithGrafanaProxyHeaders()
    {
        var connectionString = Environment.GetEnvironmentVariable("DEVCONTROL_TEST_CONNECTION_STRING");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return;
        }

        await using var upstream = await RecordingUpstream.StartAsync();
        await using var factory = new DevControlObservabilityProxyFactory(connectionString, upstream.Url);
        await factory.MigrateAsync();

        using var client = await factory.CreateAuthenticatedClientAsync("proxy-owner@example.test", "Proxy Owner");
        _ = await PostJsonAsync<OrganizationDto>(
            client,
            "/api/organizations",
            new { name = $"Proxy Org {Guid.NewGuid():N}", slug = "" });

        var response = await client.GetAsync("/observability/api/health?demo=1");
        var received = await upstream.ReceiveAsync();

        response.EnsureSuccessStatusCode();
        Assert.Equal("proxied:/observability/api/health?demo=1", await response.Content.ReadAsStringAsync());
        Assert.Equal("/observability/api/health?demo=1", received.RawUrl);
        Assert.Equal("PROXY-OWNER@EXAMPLE.TEST", received.User);
        Assert.Equal("proxy-owner@example.test", received.Email);
        Assert.Equal("Proxy Owner", received.Name);
        Assert.Equal("Owner", received.Role);
        Assert.Null(received.Authorization);
        Assert.Null(received.Cookie);
    }

    [Fact]
    public async Task ObservabilityProxy_RedirectsUnauthenticatedUsersToLogin()
    {
        await using var upstream = await RecordingUpstream.StartAsync();
        await using var factory = new DevControlObservabilityProxyFactory(
            "Host=127.0.0.1;Port=65432;Database=missing;Username=missing;Password=missing",
            upstream.Url);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        var response = await client.GetAsync("/observability/");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.StartsWith("http://localhost/auth/login", response.Headers.Location?.ToString(), StringComparison.Ordinal);
    }

    private static async Task<T> PostJsonAsync<T>(HttpClient client, string path, object payload)
    {
        var csrf = await client.GetFromJsonAsync<CsrfDto>("/api/auth/csrf");
        using var request = new HttpRequestMessage(HttpMethod.Post, path)
        {
            Content = JsonContent.Create(payload)
        };
        request.Headers.Add("X-CSRF-TOKEN", csrf?.Token ?? throw new InvalidOperationException("Missing CSRF token."));

        var response = await client.SendAsync(request);
        response.EnsureSuccessStatusCode();
        var value = await response.Content.ReadFromJsonAsync<T>();
        return value ?? throw new InvalidOperationException($"Response from {path} was empty.");
    }

    private sealed class DevControlObservabilityProxyFactory : WebApplicationFactory<Program>
    {
        private readonly string? originalConnectionString;
        private readonly string? originalObservabilityUrl;
        private readonly string? originalObservabilityRequiresToken;

        public DevControlObservabilityProxyFactory(string connectionString, string observabilityUrl)
        {
            originalConnectionString = Environment.GetEnvironmentVariable("ConnectionStrings__DevControl");
            originalObservabilityUrl = Environment.GetEnvironmentVariable("DEVCONTROL_OBSERVABILITY_UPSTREAM_URL");
            originalObservabilityRequiresToken = Environment.GetEnvironmentVariable("DEVCONTROL_OBSERVABILITY_PROXY_REQUIRES_ID_TOKEN");

            Environment.SetEnvironmentVariable("ConnectionStrings__DevControl", connectionString);
            Environment.SetEnvironmentVariable("DEVCONTROL_OBSERVABILITY_UPSTREAM_URL", observabilityUrl);
            Environment.SetEnvironmentVariable("DEVCONTROL_OBSERVABILITY_PROXY_REQUIRES_ID_TOKEN", "false");
        }

        public async Task MigrateAsync()
        {
            await using var scope = Services.CreateAsyncScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<DevControlDbContext>();
            await dbContext.Database.MigrateAsync();
        }

        public async Task<HttpClient> CreateAuthenticatedClientAsync(string email, string name)
        {
            var client = CreateClient(new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false,
                HandleCookies = true
            });

            var response = await client.GetAsync($"/auth/login?email={WebUtility.UrlEncode(email)}&name={WebUtility.UrlEncode(name)}");
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            return client;
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Test");
        }

        protected override void Dispose(bool disposing)
        {
            Environment.SetEnvironmentVariable("ConnectionStrings__DevControl", originalConnectionString);
            Environment.SetEnvironmentVariable("DEVCONTROL_OBSERVABILITY_UPSTREAM_URL", originalObservabilityUrl);
            Environment.SetEnvironmentVariable("DEVCONTROL_OBSERVABILITY_PROXY_REQUIRES_ID_TOKEN", originalObservabilityRequiresToken);
            base.Dispose(disposing);
        }
    }

    private sealed class RecordingUpstream : IAsyncDisposable
    {
        private readonly HttpListener listener;
        private readonly TaskCompletionSource<ReceivedRequest> received = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly Task listenTask;

        private RecordingUpstream(HttpListener listener, string url)
        {
            this.listener = listener;
            Url = url;
            listenTask = Task.Run(ListenAsync);
        }

        public string Url { get; }

        public static Task<RecordingUpstream> StartAsync()
        {
            var port = ReservePort();
            var url = $"http://127.0.0.1:{port}/";
            var listener = new HttpListener();
            listener.Prefixes.Add(url);
            listener.Start();
            return Task.FromResult(new RecordingUpstream(listener, url));
        }

        public async Task<ReceivedRequest> ReceiveAsync()
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            await using (timeout.Token.Register(() => received.TrySetCanceled(timeout.Token)))
            {
                return await received.Task;
            }
        }

        public async ValueTask DisposeAsync()
        {
            listener.Close();
            try
            {
                await listenTask;
            }
            catch (HttpListenerException)
            {
            }
            catch (ObjectDisposedException)
            {
            }
        }

        private async Task ListenAsync()
        {
            var context = await listener.GetContextAsync();
            var request = context.Request;
            received.TrySetResult(new ReceivedRequest(
                request.RawUrl ?? string.Empty,
                request.Headers["X-WEBAUTH-USER"],
                request.Headers["X-WEBAUTH-EMAIL"],
                request.Headers["X-WEBAUTH-NAME"],
                request.Headers["X-WEBAUTH-ROLE"],
                request.Headers["Authorization"],
                request.Headers["Cookie"]));

            var body = Encoding.UTF8.GetBytes($"proxied:{request.RawUrl}");
            context.Response.StatusCode = StatusCodes.Status200OK;
            context.Response.ContentType = "text/plain";
            context.Response.ContentLength64 = body.Length;
            await context.Response.OutputStream.WriteAsync(body);
            context.Response.Close();
        }

        private static int ReservePort()
        {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            var port = ((IPEndPoint)listener.LocalEndpoint).Port;
            listener.Stop();
            return port;
        }
    }

    private sealed record ReceivedRequest(
        string RawUrl,
        string? User,
        string? Email,
        string? Name,
        string? Role,
        string? Authorization,
        string? Cookie);

    private sealed record CsrfDto(string Token);

    private sealed record OrganizationDto(Guid Id, string Name, string Slug, string Role);
}
