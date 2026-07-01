namespace DevControl.Sdk;

public sealed class DevControlFlagSnapshot
{
    public DevControlFlagSnapshot(
        string version,
        string etag,
        IReadOnlyDictionary<string, bool> flags,
        IReadOnlyDictionary<string, bool> killSwitches,
        DateTimeOffset refreshedAt)
    {
        Version = version;
        ETag = etag;
        Flags = new Dictionary<string, bool>(flags, StringComparer.Ordinal);
        KillSwitches = new Dictionary<string, bool>(killSwitches, StringComparer.Ordinal);
        RefreshedAt = refreshedAt;
    }

    public string Version { get; }

    public string ETag { get; }

    public IReadOnlyDictionary<string, bool> Flags { get; }

    public IReadOnlyDictionary<string, bool> KillSwitches { get; }

    public DateTimeOffset RefreshedAt { get; }
}
