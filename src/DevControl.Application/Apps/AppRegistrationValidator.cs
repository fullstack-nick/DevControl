using System.Text.Json;
using System.Text.RegularExpressions;

namespace DevControl.Application.Apps;

public sealed record AppRegistrationInput(
    string? Repo,
    string? Environment,
    string? ServiceUrl,
    string? HealthUrl,
    string? CommitSha,
    string? Version,
    string? ImageDigest,
    IReadOnlyList<string>? Capabilities);

public sealed record AppRegistrationDetails(
    string Repo,
    string NormalizedRepo,
    string Environment,
    string ServiceUrl,
    string HealthUrl,
    string CommitSha,
    string Version,
    string ImageDigest,
    IReadOnlyList<string> Capabilities,
    string CapabilitiesJson);

public sealed record AppRegistrationValidationResult(AppRegistrationDetails? Details, IReadOnlyList<string> Errors)
{
    public bool IsValid => Errors.Count == 0 && Details is not null;
}

public static partial class AppRegistrationValidator
{
    public static readonly IReadOnlySet<string> KnownCapabilities = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "health",
        "flags",
        "kill-switches",
        "deploy",
        "redeploy",
        "rollback",
        "deployment-events",
        "runtime-events"
    };

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static AppRegistrationValidationResult Validate(AppRegistrationInput input)
    {
        var errors = new List<string>();
        var repo = (input.Repo ?? string.Empty).Trim();
        var environment = (input.Environment ?? string.Empty).Trim();
        var serviceUrl = (input.ServiceUrl ?? string.Empty).Trim();
        var healthUrl = (input.HealthUrl ?? string.Empty).Trim();
        var commitSha = (input.CommitSha ?? string.Empty).Trim();
        var version = (input.Version ?? string.Empty).Trim();
        var imageDigest = (input.ImageDigest ?? string.Empty).Trim();
        var normalizedCapabilities = NormalizeCapabilities(input.Capabilities, errors);

        if (!RepoRegex().IsMatch(repo))
        {
            errors.Add("Repo must use owner/name format.");
        }

        if (string.IsNullOrWhiteSpace(environment))
        {
            errors.Add("Environment is required.");
        }

        if (!IsHttpUrl(serviceUrl))
        {
            errors.Add("Service URL must be an absolute http or https URL.");
        }

        if (!IsHttpUrl(healthUrl))
        {
            errors.Add("Health URL must be an absolute http or https URL.");
        }

        if (!CommitShaRegex().IsMatch(commitSha))
        {
            errors.Add("Commit SHA must be a 7 to 64 character hexadecimal value.");
        }

        if (string.IsNullOrWhiteSpace(version))
        {
            errors.Add("Version is required.");
        }

        if (string.IsNullOrWhiteSpace(imageDigest))
        {
            errors.Add("Image digest is required.");
        }

        if (errors.Count > 0)
        {
            return new AppRegistrationValidationResult(null, errors);
        }

        var capabilitiesJson = JsonSerializer.Serialize(normalizedCapabilities, JsonOptions);
        var details = new AppRegistrationDetails(
            repo,
            repo.ToLowerInvariant(),
            environment,
            serviceUrl,
            healthUrl,
            commitSha.ToLowerInvariant(),
            version,
            imageDigest,
            normalizedCapabilities,
            capabilitiesJson);

        return new AppRegistrationValidationResult(details, []);
    }

    private static IReadOnlyList<string> NormalizeCapabilities(IReadOnlyList<string>? capabilities, List<string> errors)
    {
        if (capabilities is null || capabilities.Count == 0)
        {
            errors.Add("At least one capability is required.");
            return [];
        }

        var normalized = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var rawCapability in capabilities)
        {
            var capability = rawCapability.Trim().ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(capability))
            {
                continue;
            }

            if (!KnownCapabilities.Contains(capability))
            {
                errors.Add($"Unsupported capability '{rawCapability}'.");
                continue;
            }

            normalized.Add(capability);
        }

        if (normalized.Count == 0)
        {
            errors.Add("At least one capability is required.");
        }

        return normalized.ToArray();
    }

    private static bool IsHttpUrl(string value)
    {
        return Uri.TryCreate(value, UriKind.Absolute, out var uri) &&
            (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps) &&
            !string.IsNullOrWhiteSpace(uri.Host);
    }

    [GeneratedRegex("^[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+$")]
    private static partial Regex RepoRegex();

    [GeneratedRegex("^[a-fA-F0-9]{7,64}$")]
    private static partial Regex CommitShaRegex();
}
