namespace DevControl.Domain.Enums;

public enum ControlActionStatus
{
    Pending = 1,
    InProgress = 2,
    Succeeded = 3,
    Failed = 4,
    PartiallyApplied = 5,
    TimedOut = 6,
    Cancelled = 7,
    Reverted = 8
}
