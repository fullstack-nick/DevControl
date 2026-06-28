using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace DevControl.Cli;

public sealed class CliRunner
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private readonly HttpClient httpClient;
    private readonly CliConfigurationStore configurationStore;
    private readonly TextWriter output;
    private readonly TextWriter error;
    private readonly Func<string, string?> environment;

    public CliRunner(
        HttpClient httpClient,
        CliConfigurationStore configurationStore,
        TextWriter output,
        TextWriter error,
        Func<string, string?>? environment = null)
    {
        this.httpClient = httpClient;
        this.configurationStore = configurationStore;
        this.output = output;
        this.error = error;
        this.environment = environment ?? Environment.GetEnvironmentVariable;
    }

    public static Task<int> RunAsync(string[] args, TextWriter output, TextWriter error)
    {
        using var httpClient = new HttpClient();
        var runner = new CliRunner(httpClient, new CliConfigurationStore(), output, error);
        return runner.RunAsync(args);
    }

    public async Task<int> RunAsync(string[] args)
    {
        if (args.Length == 0 || IsHelp(args[0]))
        {
            WriteHelp();
            return 0;
        }

        try
        {
            return args[0] switch
            {
                "config" => await RunConfigAsync(args[1..]),
                "apps" => await RunAppsAsync(args[1..]),
                _ => Fail($"Unknown command '{args[0]}'.")
            };
        }
        catch (CliUsageException exception)
        {
            return Fail(exception.Message);
        }
        catch (HttpRequestException exception)
        {
            return Fail(exception.Message);
        }
        catch (TaskCanceledException)
        {
            return Fail("Request timed out.");
        }
    }

    private Task<int> RunConfigAsync(string[] args)
    {
        if (args.Length == 0 || IsHelp(args[0]))
        {
            WriteConfigHelp();
            return Task.FromResult(0);
        }

        return args[0] switch
        {
            "set" => ConfigSetAsync(args[1..]),
            "show" => ConfigShowAsync(args[1..]),
            "clear" => ConfigClearAsync(args[1..]),
            _ => Task.FromResult(Fail($"Unknown config command '{args[0]}'.")),
        };
    }

    private async Task<int> ConfigSetAsync(string[] args)
    {
        var options = ParsedArgs.Parse(args);
        var server = options.Required("server");
        var token = options.Required("token");

        if (!Uri.TryCreate(server, UriKind.Absolute, out var serverUri) ||
            (serverUri.Scheme != Uri.UriSchemeHttp && serverUri.Scheme != Uri.UriSchemeHttps))
        {
            throw new CliUsageException("Server must be an absolute http or https URL.");
        }

        await configurationStore.SaveAsync(new CliConfiguration(serverUri.ToString().TrimEnd('/'), token));
        await output.WriteLineAsync("DevControl config saved.");
        return 0;
    }

    private async Task<int> ConfigShowAsync(string[] args)
    {
        var options = ParsedArgs.Parse(args);
        var configuration = await configurationStore.LoadAsync();
        var payload = new
        {
            server = ResolveServer(options, configuration),
            hasToken = !string.IsNullOrWhiteSpace(ResolveToken(options, configuration)),
            tokenPrefix = TokenPrefix(ResolveToken(options, configuration))
        };

        if (options.HasFlag("json"))
        {
            await output.WriteLineAsync(JsonSerializer.Serialize(payload, JsonOptions));
        }
        else
        {
            await output.WriteLineAsync($"Server: {payload.server ?? "(not set)"}");
            await output.WriteLineAsync($"Token: {(payload.hasToken ? $"{payload.tokenPrefix}..." : "(not set)")}");
        }

        return 0;
    }

    private async Task<int> ConfigClearAsync(string[] args)
    {
        _ = ParsedArgs.Parse(args);
        await configurationStore.ClearAsync();
        await output.WriteLineAsync("DevControl config cleared.");
        return 0;
    }

    private Task<int> RunAppsAsync(string[] args)
    {
        if (args.Length == 0 || IsHelp(args[0]))
        {
            WriteAppsHelp();
            return Task.FromResult(0);
        }

        return args[0] switch
        {
            "register" => AppsRegisterAsync(args[1..]),
            _ => Task.FromResult(Fail($"Unknown apps command '{args[0]}'.")),
        };
    }

    private async Task<int> AppsRegisterAsync(string[] args)
    {
        var options = ParsedArgs.Parse(args);
        var configuration = await configurationStore.LoadAsync();
        var server = ResolveServer(options, configuration)
            ?? throw new CliUsageException("Server is required. Use --server, DEVCONTROL_SERVER, or devcontrol config set.");
        var token = ResolveToken(options, configuration)
            ?? throw new CliUsageException("Token is required. Use --token, DEVCONTROL_TOKEN, or devcontrol config set.");

        var payload = new AppRegisterPayload(
            options.Value("repo") ?? environment("GITHUB_REPOSITORY") ?? throw new CliUsageException("Repo is required. Use --repo or GITHUB_REPOSITORY."),
            options.Required("environment"),
            options.Required("service-url"),
            options.Required("health-url"),
            options.Value("commit-sha") ?? environment("GITHUB_SHA") ?? throw new CliUsageException("Commit SHA is required. Use --commit-sha or GITHUB_SHA."),
            options.Required("version"),
            options.Required("image-digest"),
            SplitCapabilities(options.Required("capabilities")));

        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(new Uri(server.TrimEnd('/') + "/"), "api/apps/register"))
        {
            Content = JsonContent.Create(payload)
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        using var response = await httpClient.SendAsync(request);
        var content = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode)
        {
            return Fail($"Registration failed with {(int)response.StatusCode}: {content}");
        }

        if (options.HasFlag("json"))
        {
            await output.WriteLineAsync(content);
        }
        else
        {
            using var document = JsonDocument.Parse(content);
            var repo = document.RootElement.GetProperty("repo").GetString();
            var environmentSlug = document.RootElement.GetProperty("environmentSlug").GetString();
            var version = document.RootElement.GetProperty("version").GetString();
            await output.WriteLineAsync($"Registered {repo} in {environmentSlug} at {version}.");
        }

        return 0;
    }

    private string? ResolveServer(ParsedArgs options, CliConfiguration? configuration)
    {
        return options.Value("server") ??
            environment("DEVCONTROL_SERVER") ??
            configuration?.Server;
    }

    private string? ResolveToken(ParsedArgs options, CliConfiguration? configuration)
    {
        return options.Value("token") ??
            environment("DEVCONTROL_TOKEN") ??
            configuration?.Token;
    }

    private static IReadOnlyList<string> SplitCapabilities(string value)
    {
        return value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    private static string? TokenPrefix(string? token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return null;
        }

        return token.Length <= 8 ? token : token[..8];
    }

    private int Fail(string message)
    {
        error.WriteLine(message);
        return 1;
    }

    private void WriteHelp()
    {
        output.WriteLine("DevControl CLI");
        output.WriteLine("Commands:");
        output.WriteLine("  devcontrol config set --server <url> --token <token>");
        output.WriteLine("  devcontrol config show [--json]");
        output.WriteLine("  devcontrol config clear");
        output.WriteLine("  devcontrol apps register --environment <slug> --service-url <url> --health-url <url> --version <version> --image-digest <digest> --capabilities <list>");
    }

    private void WriteConfigHelp()
    {
        output.WriteLine("Usage: devcontrol config set|show|clear");
    }

    private void WriteAppsHelp()
    {
        output.WriteLine("Usage: devcontrol apps register [options]");
    }

    private static bool IsHelp(string value)
    {
        return value is "-h" or "--help" or "help";
    }

    private sealed record AppRegisterPayload(
        string Repo,
        string Environment,
        string ServiceUrl,
        string HealthUrl,
        string CommitSha,
        string Version,
        string ImageDigest,
        IReadOnlyList<string> Capabilities);
}

public sealed class CliUsageException(string message) : Exception(message);
