namespace DevControl.Sdk;

public sealed record DevControlRefreshResult(
    DevControlRefreshStatus Status,
    string? SnapshotVersion,
    string? Error)
{
    public bool IsSuccess => Status is DevControlRefreshStatus.Updated or DevControlRefreshStatus.NotModified or DevControlRefreshStatus.Skipped;

    public static DevControlRefreshResult Updated(string snapshotVersion) => new(DevControlRefreshStatus.Updated, snapshotVersion, null);

    public static DevControlRefreshResult NotModified(string snapshotVersion) => new(DevControlRefreshStatus.NotModified, snapshotVersion, null);

    public static DevControlRefreshResult Skipped(string? snapshotVersion) => new(DevControlRefreshStatus.Skipped, snapshotVersion, null);

    public static DevControlRefreshResult Failed(string? snapshotVersion, string error) => new(DevControlRefreshStatus.Failed, snapshotVersion, error);
}

public enum DevControlRefreshStatus
{
    Updated,
    NotModified,
    Skipped,
    Failed
}
