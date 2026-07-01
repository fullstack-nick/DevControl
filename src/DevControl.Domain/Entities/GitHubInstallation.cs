namespace DevControl.Domain.Entities;

public sealed class GitHubInstallation
{
    public Guid Id { get; private set; }

    public Guid OrganizationId { get; private set; }

    public long InstallationId { get; private set; }

    public string AccountLogin { get; private set; } = string.Empty;

    public string AccountType { get; private set; } = string.Empty;

    public string RepositorySelection { get; private set; } = string.Empty;

    public string PermissionsJson { get; private set; } = "{}";

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    private GitHubInstallation()
    {
    }

    public GitHubInstallation(
        Guid organizationId,
        long installationId,
        string accountLogin,
        string accountType,
        string repositorySelection,
        string permissionsJson,
        DateTimeOffset now)
    {
        Id = Guid.NewGuid();
        OrganizationId = organizationId;
        InstallationId = installationId;
        CreatedAt = now;
        Update(accountLogin, accountType, repositorySelection, permissionsJson, now);
    }

    public void Update(string accountLogin, string accountType, string repositorySelection, string permissionsJson, DateTimeOffset now)
    {
        AccountLogin = Require(accountLogin, nameof(accountLogin), 160);
        AccountType = Require(accountType, nameof(accountType), 40);
        RepositorySelection = Require(repositorySelection, nameof(repositorySelection), 40);
        PermissionsJson = string.IsNullOrWhiteSpace(permissionsJson) ? "{}" : permissionsJson.Trim();
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
