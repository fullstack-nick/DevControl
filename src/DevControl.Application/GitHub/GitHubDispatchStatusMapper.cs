using DevControl.Domain.Enums;

namespace DevControl.Application.GitHub;

public static class GitHubDispatchStatusMapper
{
    public static bool IsTerminal(string? status)
    {
        return string.Equals(status, "completed", StringComparison.OrdinalIgnoreCase);
    }

    public static ControlActionStatus ToControlActionStatus(string? status, string? conclusion)
    {
        if (!IsTerminal(status))
        {
            return ControlActionStatus.InProgress;
        }

        return (conclusion ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "success" => ControlActionStatus.Succeeded,
            "cancelled" => ControlActionStatus.Cancelled,
            "timed_out" => ControlActionStatus.TimedOut,
            _ => ControlActionStatus.Failed
        };
    }
}
