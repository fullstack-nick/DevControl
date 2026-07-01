namespace DevControl.Domain.Entities;

public sealed class GitHubOnboardingPullRequest
{
    public Guid Id { get; private set; }

    public Guid OrganizationId { get; private set; }

    public Guid ProjectId { get; private set; }

    public Guid EnvironmentId { get; private set; }

    public Guid RepoConnectionId { get; private set; }

    public string Repo { get; private set; } = string.Empty;

    public string WorkflowPath { get; private set; } = string.Empty;

    public string BaseBranch { get; private set; } = string.Empty;

    public string HeadBranch { get; private set; } = string.Empty;

    public int PullRequestNumber { get; private set; }

    public string PullRequestUrl { get; private set; } = string.Empty;

    public string Status { get; private set; } = string.Empty;

    public string Error { get; private set; } = string.Empty;

    public Guid CreatedByUserId { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public DateTimeOffset? MergedAt { get; private set; }

    public DateTimeOffset? ClosedAt { get; private set; }

    private GitHubOnboardingPullRequest()
    {
    }

    public GitHubOnboardingPullRequest(
        Guid organizationId,
        Guid projectId,
        Guid environmentId,
        Guid repoConnectionId,
        string repo,
        string workflowPath,
        string baseBranch,
        string headBranch,
        int pullRequestNumber,
        string pullRequestUrl,
        Guid createdByUserId,
        DateTimeOffset now)
    {
        Id = Guid.NewGuid();
        OrganizationId = organizationId;
        ProjectId = projectId;
        EnvironmentId = environmentId;
        RepoConnectionId = repoConnectionId;
        Repo = Require(repo, nameof(repo), 220);
        WorkflowPath = Require(workflowPath, nameof(workflowPath), 300);
        BaseBranch = Require(baseBranch, nameof(baseBranch), 160);
        HeadBranch = Require(headBranch, nameof(headBranch), 200);
        PullRequestNumber = pullRequestNumber;
        PullRequestUrl = Require(pullRequestUrl, nameof(pullRequestUrl), 500);
        Status = "Open";
        CreatedByUserId = createdByUserId;
        CreatedAt = now;
        UpdatedAt = now;
    }

    public void MarkSynced(string status, DateTimeOffset? mergedAt, DateTimeOffset? closedAt, string error, DateTimeOffset now)
    {
        Status = Require(status, nameof(status), 40);
        MergedAt = mergedAt;
        ClosedAt = closedAt;
        Error = string.IsNullOrWhiteSpace(error) ? string.Empty : error.Trim();
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
