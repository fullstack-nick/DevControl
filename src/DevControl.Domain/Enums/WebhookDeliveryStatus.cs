namespace DevControl.Domain.Enums;

public enum WebhookDeliveryStatus
{
    Pending = 0,
    Succeeded = 1,
    Failed = 2,
    Exhausted = 3,
    SkippedPaused = 4
}
