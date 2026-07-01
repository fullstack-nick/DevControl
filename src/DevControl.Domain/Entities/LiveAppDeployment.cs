namespace DevControl.Domain.Entities;

public sealed class LiveAppDeployment
{
    public Guid Id { get; private set; }

    public Guid LiveAppId { get; private set; }

    public Guid OrganizationId { get; private set; }

    public Guid ProjectId { get; private set; }

    public Guid EnvironmentId { get; private set; }

    public string Repo { get; private set; } = string.Empty;

    public string ServiceUrl { get; private set; } = string.Empty;

    public string HealthUrl { get; private set; } = string.Empty;

    public string CommitSha { get; private set; } = string.Empty;

    public string Version { get; private set; } = string.Empty;

    public string ImageDigest { get; private set; } = string.Empty;

    public string CapabilitiesJson { get; private set; } = "[]";

    public long? GitHubRunId { get; private set; }

    public string GitHubRunUrl { get; private set; } = string.Empty;

    public DateTimeOffset RegisteredAt { get; private set; }

    private LiveAppDeployment()
    {
    }

    public LiveAppDeployment(
        Guid liveAppId,
        Guid organizationId,
        Guid projectId,
        Guid environmentId,
        string repo,
        string serviceUrl,
        string healthUrl,
        string commitSha,
        string version,
        string imageDigest,
        string capabilitiesJson,
        long? gitHubRunId,
        string? gitHubRunUrl,
        DateTimeOffset now)
    {
        Id = Guid.NewGuid();
        LiveAppId = liveAppId;
        OrganizationId = organizationId;
        ProjectId = projectId;
        EnvironmentId = environmentId;
        Repo = Require(repo, nameof(repo));
        ServiceUrl = Require(serviceUrl, nameof(serviceUrl));
        HealthUrl = Require(healthUrl, nameof(healthUrl));
        CommitSha = Require(commitSha, nameof(commitSha));
        Version = Require(version, nameof(version));
        ImageDigest = Require(imageDigest, nameof(imageDigest));
        CapabilitiesJson = string.IsNullOrWhiteSpace(capabilitiesJson) ? "[]" : capabilitiesJson.Trim();
        GitHubRunId = gitHubRunId;
        GitHubRunUrl = string.IsNullOrWhiteSpace(gitHubRunUrl) ? string.Empty : gitHubRunUrl.Trim();
        RegisteredAt = now;
    }

    private static string Require(string value, string paramName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException($"{paramName} is required.", paramName);
        }

        return value.Trim();
    }
}
