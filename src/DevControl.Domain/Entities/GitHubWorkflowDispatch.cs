namespace DevControl.Domain.Entities;

public sealed class GitHubWorkflowDispatch
{
    public Guid Id { get; private set; }

    public Guid OrganizationId { get; private set; }

    public Guid ProjectId { get; private set; }

    public Guid EnvironmentId { get; private set; }

    public Guid RepoConnectionId { get; private set; }

    public Guid LiveAppId { get; private set; }

    public Guid ControlActionId { get; private set; }

    public string Action { get; private set; } = string.Empty;

    public string Repo { get; private set; } = string.Empty;

    public string WorkflowPath { get; private set; } = string.Empty;

    public string Ref { get; private set; } = string.Empty;

    public long? GitHubRunId { get; private set; }

    public string RunUrl { get; private set; } = string.Empty;

    public string Status { get; private set; } = string.Empty;

    public string Conclusion { get; private set; } = string.Empty;

    public string InputsJson { get; private set; } = "{}";

    public Guid RequestedByUserId { get; private set; }

    public DateTimeOffset RequestedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public DateTimeOffset? CompletedAt { get; private set; }

    private GitHubWorkflowDispatch()
    {
    }

    public GitHubWorkflowDispatch(
        Guid organizationId,
        Guid projectId,
        Guid environmentId,
        Guid repoConnectionId,
        Guid liveAppId,
        Guid controlActionId,
        string action,
        string repo,
        string workflowPath,
        string gitRef,
        long? gitHubRunId,
        string runUrl,
        string inputsJson,
        Guid requestedByUserId,
        DateTimeOffset now)
    {
        Id = Guid.NewGuid();
        OrganizationId = organizationId;
        ProjectId = projectId;
        EnvironmentId = environmentId;
        RepoConnectionId = repoConnectionId;
        LiveAppId = liveAppId;
        ControlActionId = controlActionId;
        Action = Require(action, nameof(action), 40);
        Repo = Require(repo, nameof(repo), 220);
        WorkflowPath = Require(workflowPath, nameof(workflowPath), 300);
        Ref = Require(gitRef, nameof(gitRef), 160);
        GitHubRunId = gitHubRunId;
        RunUrl = string.IsNullOrWhiteSpace(runUrl) ? string.Empty : runUrl.Trim();
        InputsJson = string.IsNullOrWhiteSpace(inputsJson) ? "{}" : inputsJson.Trim();
        Status = gitHubRunId.HasValue ? "queued" : "dispatched";
        Conclusion = string.Empty;
        RequestedByUserId = requestedByUserId;
        RequestedAt = now;
        UpdatedAt = now;
    }

    public void UpdateRun(long? gitHubRunId, string runUrl, string status, string conclusion, DateTimeOffset? completedAt, DateTimeOffset now)
    {
        if (gitHubRunId.HasValue)
        {
            GitHubRunId = gitHubRunId;
        }

        if (!string.IsNullOrWhiteSpace(runUrl))
        {
            RunUrl = runUrl.Trim();
        }

        Status = string.IsNullOrWhiteSpace(status) ? Status : status.Trim();
        Conclusion = string.IsNullOrWhiteSpace(conclusion) ? string.Empty : conclusion.Trim();
        CompletedAt = completedAt;
        UpdatedAt = now;
    }

    private static string Require(string value, string paramName, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException($"{paramName} is required.", paramName);
        }

        value = value.Trim();
        if (value.Length > maxLength)
        {
            throw new ArgumentException($"{paramName} cannot exceed {maxLength} characters.", paramName);
        }

        return value;
    }
}
