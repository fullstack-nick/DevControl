using System.Net;
using System.Text.Json;
using DevControl.Cli;
using Xunit;

namespace DevControl.UnitTests;

public sealed class CliRunnerTests
{
    [Fact]
    public async Task ConfigShow_RedactsStoredToken()
    {
        var configPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.json");
        var output = new StringWriter();
        var error = new StringWriter();
        var store = new CliConfigurationStore(configPath);
        await store.SaveAsync(new CliConfiguration("https://devcontrol.example.com", "dcr_super_secret_token"));

        var runner = new CliRunner(new HttpClient(new RecordingHandler()), store, output, error, _ => null);

        var exitCode = await runner.RunAsync(["config", "show", "--json"]);

        Assert.Equal(0, exitCode);
        Assert.DoesNotContain("super_secret", output.ToString(), StringComparison.Ordinal);
        Assert.Contains("\"hasToken\": true", output.ToString(), StringComparison.Ordinal);
        File.Delete(configPath);
    }

    [Fact]
    public async Task AppsRegister_UsesEnvironmentDefaultsAndPostsJson()
    {
        var output = new StringWriter();
        var error = new StringWriter();
        var handler = new RecordingHandler();
        var environment = new Dictionary<string, string?>
        {
            ["DEVCONTROL_SERVER"] = "https://devcontrol.example.com",
            ["DEVCONTROL_TOKEN"] = "dcr_token",
            ["GITHUB_REPOSITORY"] = "fullstack-nick/sample",
            ["GITHUB_SHA"] = "abcdef1234567890"
        };
        var runner = new CliRunner(
            new HttpClient(handler),
            new CliConfigurationStore(Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.json")),
            output,
            error,
            key => environment.GetValueOrDefault(key));

        var exitCode = await runner.RunAsync([
            "apps",
            "register",
            "--environment",
            "production",
            "--service-url",
            "https://sample.example.com",
            "--health-url",
            "https://sample.example.com/health",
            "--version",
            "v1",
            "--image-digest",
            "sha256:abc",
            "--capabilities",
            "health,deployment-events",
            "--json"
        ]);

        Assert.Equal(0, exitCode);
        Assert.Equal(HttpMethod.Post, handler.Request!.Method);
        Assert.Equal("https://devcontrol.example.com/api/apps/register", handler.Request.RequestUri!.ToString());
        Assert.Equal("Bearer", handler.Request.Headers.Authorization!.Scheme);
        Assert.Equal("dcr_token", handler.Request.Headers.Authorization.Parameter);
        Assert.Contains("\"repo\":\"fullstack-nick/sample\"", handler.Body, StringComparison.Ordinal);
        Assert.Contains("\"commitSha\":\"abcdef1234567890\"", handler.Body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AdminBootstrapLiveProof_UsesOperatorHeaderAndDoesNotLogSecret()
    {
        var output = new StringWriter();
        var error = new StringWriter();
        var handler = new RecordingHandler();
        var environment = new Dictionary<string, string?>
        {
            ["DEVCONTROL_SERVER"] = "https://devcontrol.example.com",
            ["DEVCONTROL_OPERATOR_BOOTSTRAP_SECRET"] = "operator-secret"
        };
        var runner = new CliRunner(
            new HttpClient(handler),
            new CliConfigurationStore(Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.json")),
            output,
            error,
            key => environment.GetValueOrDefault(key));

        var exitCode = await runner.RunAsync([
            "admin",
            "bootstrap-live-proof",
            "--owner-email",
            "owner@example.com",
            "--json"
        ]);

        Assert.Equal(0, exitCode);
        Assert.Equal(HttpMethod.Post, handler.Request!.Method);
        Assert.Equal("https://devcontrol.example.com/api/operator/bootstrap-live-proof", handler.Request.RequestUri!.ToString());
        Assert.True(handler.Request.Headers.TryGetValues("X-DevControl-Operator-Secret", out var headerValues));
        Assert.Equal("operator-secret", Assert.Single(headerValues));
        Assert.Contains("\"ownerEmail\":\"owner@example.com\"", handler.Body, StringComparison.Ordinal);
        Assert.DoesNotContain("operator-secret", output.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("operator-secret", error.ToString(), StringComparison.Ordinal);
    }

    private sealed class RecordingHandler : HttpMessageHandler
    {
        public HttpRequestMessage? Request { get; private set; }

        public string Body { get; private set; } = string.Empty;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Request = request;
            Body = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(ResponseJson(request))
            };
        }

        private static string ResponseJson(HttpRequestMessage request)
        {
            if (request.RequestUri!.AbsolutePath.EndsWith("/api/operator/bootstrap-live-proof", StringComparison.Ordinal))
            {
                return JsonSerializer.Serialize(new
                {
                    owner = new { id = Guid.NewGuid(), email = "owner@example.com", displayName = "owner@example.com" },
                    organization = new { id = Guid.NewGuid(), name = "Acme Platform", slug = "acme-platform" },
                    project = new { id = Guid.NewGuid(), name = "Sample App", slug = "sample-app" },
                    environment = new { id = Guid.NewGuid(), name = "Production", slug = "production" },
                    registrationToken = new { id = Guid.NewGuid(), name = "Operator bootstrap registration token", tokenPrefix = "dcr_prefix", scope = "apps:register", secret = "dcr_show_once" },
                    apiKey = new { id = Guid.NewGuid(), name = "Operator bootstrap API key", keyPrefix = "dck_prefix", scopes = new[] { "flags:read", "sample:read" }, rateLimitPerMinute = 10, secret = "dck_show_once" },
                    revokedRegistrationTokenIds = Array.Empty<Guid>(),
                    revokedApiKeyIds = Array.Empty<Guid>()
                });
            }

            return JsonSerializer.Serialize(new
            {
                repo = "fullstack-nick/sample",
                environmentSlug = "production",
                version = "v1"
            });
        }
    }
}
