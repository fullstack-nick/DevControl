using DevControl.Application.GitHub;

namespace DevControl.Api.GitHub;

public interface IGitHubAppClient
{
    bool IsConfigured { get; }

    Task<GitHubInstallationInfo?> GetRepositoryInstallationAsync(GitHubRepoName repo, CancellationToken cancellationToken);

    Task<GitHubRepositoryInfo> GetRepositoryAsync(GitHubRepoName repo, long installationId, CancellationToken cancellationToken);

    Task<IReadOnlyList<GitHubWorkflowInfo>> ListWorkflowsAsync(GitHubRepoName repo, long installationId, CancellationToken cancellationToken);

    Task<GitHubFileContent> GetFileContentAsync(GitHubRepoName repo, long installationId, string path, string gitRef, CancellationToken cancellationToken);

    Task<GitHubPullRequestInfo> CreateOnboardingPullRequestAsync(
        GitHubRepoName repo,
        long installationId,
        string baseBranch,
        string headBranch,
        string workflowPath,
        string currentFileSha,
        string patchedContent,
        string title,
        string body,
        CancellationToken cancellationToken);

    Task<GitHubPullRequestState?> GetPullRequestAsync(GitHubRepoName repo, long installationId, int pullRequestNumber, CancellationToken cancellationToken);

    Task<GitHubWorkflowDispatchInfo> DispatchWorkflowAsync(
        GitHubRepoName repo,
        long installationId,
        string workflowPath,
        string gitRef,
        IReadOnlyDictionary<string, string> inputs,
        DateTimeOffset requestedAt,
        CancellationToken cancellationToken);

    Task<GitHubWorkflowRunInfo?> GetWorkflowRunAsync(GitHubRepoName repo, long installationId, long runId, CancellationToken cancellationToken);

    Task<GitHubWorkflowRunInfo?> FindWorkflowRunAsync(
        GitHubRepoName repo,
        long installationId,
        string workflowPath,
        string gitRef,
        DateTimeOffset requestedAt,
        CancellationToken cancellationToken);
}

public sealed record GitHubInstallationInfo(
    long InstallationId,
    string AccountLogin,
    string AccountType,
    string RepositorySelection,
    string PermissionsJson);

public sealed record GitHubRepositoryInfo(string FullName, string DefaultBranch, string HtmlUrl);

public sealed record GitHubWorkflowInfo(long Id, string Name, string Path, string State);

public sealed record GitHubFileContent(string Path, string Sha, string Content);

public sealed record GitHubPullRequestInfo(int Number, string Url);

public sealed record GitHubPullRequestState(string State, bool Merged, DateTimeOffset? MergedAt, DateTimeOffset? ClosedAt);

public sealed record GitHubWorkflowDispatchInfo(long? RunId, string RunUrl);

public sealed record GitHubWorkflowRunInfo(long Id, string Url, string Status, string Conclusion, DateTimeOffset? CompletedAt);
