namespace DevControl.Api.GitHub;

public interface IGitHubOidcTokenValidator
{
    Task<GitHubOidcClaims?> ValidateAsync(string token, string expectedAudience, CancellationToken cancellationToken);
}

public sealed record GitHubOidcClaims(
    string Repository,
    string Ref,
    string WorkflowRef,
    string WorkflowSha,
    string RunId,
    string Actor,
    string EventName);
