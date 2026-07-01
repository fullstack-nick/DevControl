namespace DevControl.Domain.Entities;

public sealed class LiveApp
{
    public Guid Id { get; private set; }

    public Guid OrganizationId { get; private set; }

    public Guid ProjectId { get; private set; }

    public Guid EnvironmentId { get; private set; }

    public string Repo { get; private set; } = string.Empty;

    public string NormalizedRepo { get; private set; } = string.Empty;

    public string ServiceUrl { get; private set; } = string.Empty;

    public string HealthUrl { get; private set; } = string.Empty;

    public string CurrentCommitSha { get; private set; } = string.Empty;

    public string Version { get; private set; } = string.Empty;

    public string ImageDigest { get; private set; } = string.Empty;

    public string CapabilitiesJson { get; private set; } = "[]";

    public long? GitHubRunId { get; private set; }

    public string GitHubRunUrl { get; private set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset LastRegisteredAt { get; private set; }

    private LiveApp()
    {
    }

    public LiveApp(
        Guid organizationId,
        Guid projectId,
        Guid environmentId,
        string repo,
        string normalizedRepo,
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
        OrganizationId = organizationId;
        ProjectId = projectId;
        EnvironmentId = environmentId;
        CreatedAt = now;
        UpdateRegistration(repo, normalizedRepo, serviceUrl, healthUrl, commitSha, version, imageDigest, capabilitiesJson, gitHubRunId, gitHubRunUrl, now);
    }

    public void UpdateRegistration(
        string repo,
        string normalizedRepo,
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
        Repo = Require(repo, nameof(repo));
        NormalizedRepo = Require(normalizedRepo, nameof(normalizedRepo));
        ServiceUrl = Require(serviceUrl, nameof(serviceUrl));
        HealthUrl = Require(healthUrl, nameof(healthUrl));
        CurrentCommitSha = Require(commitSha, nameof(commitSha));
        Version = Require(version, nameof(version));
        ImageDigest = Require(imageDigest, nameof(imageDigest));
        CapabilitiesJson = string.IsNullOrWhiteSpace(capabilitiesJson) ? "[]" : capabilitiesJson.Trim();
        GitHubRunId = gitHubRunId;
        GitHubRunUrl = string.IsNullOrWhiteSpace(gitHubRunUrl) ? string.Empty : gitHubRunUrl.Trim();
        LastRegisteredAt = now;
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
