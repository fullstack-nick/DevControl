namespace DevControl.Application.Health;

public sealed record HealthPayload(
    string Status,
    string Service,
    DateTimeOffset TimestampUtc,
    string? Dependency = null)
{
    public static HealthPayload Live() => new("live", "DevControl", DateTimeOffset.UtcNow);

    public static HealthPayload Ready() => new("ready", "DevControl", DateTimeOffset.UtcNow, "postgresql");

    public static HealthPayload NotReady() => new("not_ready", "DevControl", DateTimeOffset.UtcNow, "postgresql");
}

