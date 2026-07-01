namespace DevControl.Sdk;

public sealed class DevControlClientOptions
{
    public TimeSpan RequestTimeout { get; init; } = TimeSpan.FromSeconds(2);

    public TimeSpan RefreshInterval { get; init; } = TimeSpan.FromSeconds(60);

    public TimeSpan KillSwitchRefreshInterval { get; init; } = TimeSpan.FromSeconds(20);
}
