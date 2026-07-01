namespace DevControl.Domain.Entities;

public sealed class GitHubRepoConnection
{
    public Guid Id { get; private set; }

    public Guid OrganizationId { get; private set; }

    public Guid ProjectId { get; private set; }

    public Guid EnvironmentId { get; private set; }

    public Guid GitHubInstallationId { get; private set; }

    public Guid? LiveAppId { get; private set; }

    public string Repo { get; private set; } = string.Empty;

    public string NormalizedRepo { get; private set; } = string.Empty;

    public string DefaultBranch { get; private set; } = string.Empty;

    public string WorkflowPath { get; private set; } = string.Empty;

    public string WorkflowName { get; private set; } = string.Empty;

    public string JobId { get; private set; } = string.Empty;

    public string ServiceUrlExpression { get; private set; } = string.Empty;

    public string HealthUrlExpression { get; private set; } = string.Empty;

    public string VersionExpression { get; private set; } = string.Empty;

    public string ImageDigestExpression { get; private set; } = string.Empty;

    public string CapabilitiesJson { get; private set; } = "[]";

    public Guid CreatedByUserId { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    private GitHubRepoConnection()
    {
    }

    public GitHubRepoConnection(
        Guid organizationId,
        Guid projectId,
        Guid environmentId,
        Guid gitHubInstallationId,
        string repo,
        string normalizedRepo,
        string defaultBranch,
        string workflowPath,
        string workflowName,
        string jobId,
        string serviceUrlExpression,
        string healthUrlExpression,
        string versionExpression,
        string imageDigestExpression,
        string capabilitiesJson,
        Guid createdByUserId,
        DateTimeOffset now)
    {
        Id = Guid.NewGuid();
        OrganizationId = organizationId;
        ProjectId = projectId;
        EnvironmentId = environmentId;
        GitHubInstallationId = gitHubInstallationId;
        CreatedByUserId = createdByUserId;
        CreatedAt = now;
        Update(
            repo,
            normalizedRepo,
            defaultBranch,
            workflowPath,
            workflowName,
            jobId,
            serviceUrlExpression,
            healthUrlExpression,
            versionExpression,
            imageDigestExpression,
            capabilitiesJson,
            now);
    }

    public void Update(
        string repo,
        string normalizedRepo,
        string defaultBranch,
        string workflowPath,
        string workflowName,
        string jobId,
        string serviceUrlExpression,
        string healthUrlExpression,
        string versionExpression,
        string imageDigestExpression,
        string capabilitiesJson,
        DateTimeOffset now)
    {
        Repo = Require(repo, nameof(repo), 220);
        NormalizedRepo = Require(normalizedRepo, nameof(normalizedRepo), 220);
        DefaultBranch = Require(defaultBranch, nameof(defaultBranch), 160);
        WorkflowPath = Require(workflowPath, nameof(workflowPath), 300);
        WorkflowName = Require(workflowName, nameof(workflowName), 160);
        JobId = Require(jobId, nameof(jobId), 120);
        ServiceUrlExpression = Require(serviceUrlExpression, nameof(serviceUrlExpression), 500);
        HealthUrlExpression = Require(healthUrlExpression, nameof(healthUrlExpression), 500);
        VersionExpression = Require(versionExpression, nameof(versionExpression), 200);
        ImageDigestExpression = Require(imageDigestExpression, nameof(imageDigestExpression), 300);
        CapabilitiesJson = string.IsNullOrWhiteSpace(capabilitiesJson) ? "[]" : capabilitiesJson.Trim();
        UpdatedAt = now;
    }

    public void LinkLiveApp(Guid liveAppId, DateTimeOffset now)
    {
        LiveAppId = liveAppId;
        UpdatedAt = now;
    }

    public void LinkInstallation(Guid gitHubInstallationId, DateTimeOffset now)
    {
        GitHubInstallationId = gitHubInstallationId;
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
