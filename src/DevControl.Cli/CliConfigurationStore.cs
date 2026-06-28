using System.Text.Json;

namespace DevControl.Cli;

public sealed record CliConfiguration(string Server, string Token);

public sealed class CliConfigurationStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private readonly string path;

    public CliConfigurationStore(string? path = null)
    {
        this.path = path ??
            Environment.GetEnvironmentVariable("DEVCONTROL_CONFIG_PATH") ??
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".devcontrol", "config.json");
    }

    public async Task<CliConfiguration?> LoadAsync()
    {
        if (!File.Exists(path))
        {
            return null;
        }

        await using var stream = File.OpenRead(path);
        return await JsonSerializer.DeserializeAsync<CliConfiguration>(stream, JsonOptions);
    }

    public async Task SaveAsync(CliConfiguration configuration)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await using var stream = File.Create(path);
        await JsonSerializer.SerializeAsync(stream, configuration, JsonOptions);
        await stream.WriteAsync("\n"u8.ToArray());
    }

    public Task ClearAsync()
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }

        return Task.CompletedTask;
    }
}
