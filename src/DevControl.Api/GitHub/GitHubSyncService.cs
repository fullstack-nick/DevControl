using System.Text.Json;
using DevControl.Application.GitHub;
using DevControl.Domain.Enums;
using DevControl.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;

namespace DevControl.Api.GitHub;

public sealed class GitHubSyncService(
    DevControlDbContext dbContext,
    IGitHubAppClient gitHubAppClient,
    TimeProvider timeProvider,
    ILogger<GitHubSyncService> logger)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly TimeSpan UncorrelatedDispatchTimeout = TimeSpan.FromMinutes(30);

    public async Task<GitHubSyncResult> SyncAsync(int pullRequestBatchSize, int dispatchBatchSize, CancellationToken cancellationToken)
    {
        if (!gitHubAppClient.IsConfigured)
        {
            return new GitHubSyncResult(0, 0, pullRequestBatchSize, dispatchBatchSize);
        }

        var pullRequests = await SyncPullRequestsAsync(pullRequestBatchSize, cancellationToken);
        var dispatches = await SyncWorkflowDispatchesAsync(dispatchBatchSize, null, cancellationToken);
        return new GitHubSyncResult(pullRequests, dispatches, pullRequestBatchSize, dispatchBatchSize);
    }

    private async Task<int> SyncPullRequestsAsync(int batchSize, CancellationToken cancellationToken)
    {
        var pullRequests = await dbContext.GitHubOnboardingPullRequests
            .Where(pullRequest => pullRequest.Status == "Open")
            .OrderBy(pullRequest => pullRequest.UpdatedAt)
            .Take(batchSize)
            .ToListAsync(cancellationToken);

        var updated = 0;
        foreach (var pullRequest in pullRequests)
        {
            var connection = await dbContext.GitHubRepoConnections
                .SingleOrDefaultAsync(candidate => candidate.Id == pullRequest.RepoConnectionId, cancellationToken);
            if (connection is null || !GitHubRepoNameParser.TryParse(pullRequest.Repo, out var repo))
            {
                continue;
            }

            var installation = await dbContext.GitHubInstallations
                .SingleOrDefaultAsync(candidate => candidate.Id == connection.GitHubInstallationId, cancellationToken);
            if (installation is null)
            {
                continue;
            }

            var now = timeProvider.GetUtcNow();
            try
            {
                var state = await gitHubAppClient.GetPullRequestAsync(repo, installation.InstallationId, pullRequest.PullRequestNumber, cancellationToken);
                if (state is null)
                {
                    pullRequest.MarkSynced("Missing", null, null, "Pull request was not found in GitHub.", now);
                }
                else
                {
                    var status = state.Merged
                        ? "Merged"
                        : state.State.Equals("closed", StringComparison.OrdinalIgnoreCase)
                            ? "Closed"
                            : "Open";
                    pullRequest.MarkSynced(status, state.MergedAt, state.ClosedAt, string.Empty, now);
                }

                updated++;
            }
            catch (Exception exception) when (exception is HttpRequestException or InvalidOperationException)
            {
                logger.LogWarning(exception, "Failed to sync GitHub onboarding pull request {PullRequestId}.", pullRequest.Id);
                pullRequest.MarkSynced(pullRequest.Status, pullRequest.MergedAt, pullRequest.ClosedAt, exception.Message, now);
                updated++;
            }
        }

        if (updated > 0)
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return updated;
    }

    public async Task<int> SyncWorkflowDispatchesAsync(int batchSize, Guid? organizationId, CancellationToken cancellationToken)
    {
        if (!gitHubAppClient.IsConfigured)
        {
            return 0;
        }

        var query = dbContext.GitHubWorkflowDispatches
            .Where(dispatch => dispatch.CompletedAt == null)
            .AsQueryable();
        if (organizationId.HasValue)
        {
            query = query.Where(dispatch => dispatch.OrganizationId == organizationId.Value);
        }

        var dispatches = await query
            .OrderBy(dispatch => dispatch.UpdatedAt)
            .Take(batchSize)
            .ToListAsync(cancellationToken);

        var updated = 0;
        foreach (var dispatch in dispatches)
        {
            var connection = await dbContext.GitHubRepoConnections
                .SingleOrDefaultAsync(candidate => candidate.Id == dispatch.RepoConnectionId, cancellationToken);
            var installation = connection is null
                ? null
                : await dbContext.GitHubInstallations.SingleOrDefaultAsync(candidate => candidate.Id == connection.GitHubInstallationId, cancellationToken);
            var controlAction = await dbContext.ControlActions
                .SingleOrDefaultAsync(candidate => candidate.Id == dispatch.ControlActionId, cancellationToken);
            if (connection is null || installation is null || controlAction is null || !GitHubRepoNameParser.TryParse(dispatch.Repo, out var repo))
            {
                continue;
            }

            var now = timeProvider.GetUtcNow();
            try
            {
                var run = dispatch.GitHubRunId.HasValue
                    ? await gitHubAppClient.GetWorkflowRunAsync(repo, installation.InstallationId, dispatch.GitHubRunId.Value, cancellationToken)
                    : await gitHubAppClient.FindWorkflowRunAsync(repo, installation.InstallationId, dispatch.WorkflowPath, dispatch.Ref, dispatch.RequestedAt, cancellationToken);

                if (run is null)
                {
                    if (now - dispatch.RequestedAt >= UncorrelatedDispatchTimeout)
                    {
                        dispatch.UpdateRun(null, dispatch.RunUrl, "completed", "timed_out", now, now);
                        controlAction.MarkCompleted(
                            ControlActionStatus.TimedOut,
                            JsonSerializer.Serialize(new { dispatch.Id, dispatch.Action, reason = "GitHub workflow run could not be correlated." }, JsonOptions),
                            null,
                            now);
                    }
                    else
                    {
                        dispatch.UpdateRun(null, dispatch.RunUrl, dispatch.Status, dispatch.Conclusion, null, now);
                    }

                    updated++;
                    continue;
                }

                var terminalStatus = GitHubDispatchStatusMapper.ToControlActionStatus(run.Status, run.Conclusion);
                DateTimeOffset? completedAt = terminalStatus == ControlActionStatus.InProgress ? null : run.CompletedAt ?? now;
                dispatch.UpdateRun(run.Id, run.Url, run.Status, run.Conclusion, completedAt, now);
                if (terminalStatus != ControlActionStatus.InProgress)
                {
                    controlAction.MarkCompleted(
                        terminalStatus,
                        JsonSerializer.Serialize(new
                        {
                            dispatch.Id,
                            dispatch.Action,
                            gitHubRunId = run.Id,
                            gitHubRunUrl = run.Url,
                            run.Status,
                            run.Conclusion
                        }, JsonOptions),
                        run.Id.ToString(),
                        completedAt ?? now);
                }

                updated++;
            }
            catch (Exception exception) when (exception is HttpRequestException or InvalidOperationException)
            {
                logger.LogWarning(exception, "Failed to sync GitHub workflow dispatch {DispatchId}.", dispatch.Id);
                dispatch.UpdateRun(dispatch.GitHubRunId, dispatch.RunUrl, dispatch.Status, exception.Message, null, now);
                updated++;
            }
        }

        if (updated > 0)
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return updated;
    }
}

public sealed record GitHubSyncResult(
    int PullRequests,
    int WorkflowDispatches,
    int PullRequestBatchSize = 0,
    int WorkflowDispatchBatchSize = 0);
