using DevControl.Application.Apps;
using DevControl.Application.Security;
using Xunit;

namespace DevControl.UnitTests;

public sealed class AppRegistrationValidatorTests
{
    [Fact]
    public void Validate_NormalizesRepoCommitAndCapabilities()
    {
        var result = AppRegistrationValidator.Validate(new AppRegistrationInput(
            "FullStack-Nick/My-App",
            "production",
            "https://app.example.com",
            "https://app.example.com/health",
            "ABCDEF1234567",
            "v1",
            "sha256:abc",
            ["deployment-events", "health", "health"]));

        Assert.True(result.IsValid);
        Assert.Equal("fullstack-nick/my-app", result.Details!.NormalizedRepo);
        Assert.Equal("abcdef1234567", result.Details.CommitSha);
        Assert.Equal(["deployment-events", "health"], result.Details.Capabilities);
        Assert.Equal("[\"deployment-events\",\"health\"]", result.Details.CapabilitiesJson);
    }

    [Fact]
    public void Validate_RejectsUnsafeOrIncompleteInput()
    {
        var result = AppRegistrationValidator.Validate(new AppRegistrationInput(
            "not-a-repo",
            "",
            "file:///tmp/app",
            "https://app.example.com/health",
            "not-sha",
            "",
            "",
            ["unknown"]));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.Contains("Repo", StringComparison.Ordinal));
        Assert.Contains(result.Errors, error => error.Contains("Environment", StringComparison.Ordinal));
        Assert.Contains(result.Errors, error => error.Contains("Service URL", StringComparison.Ordinal));
        Assert.Contains(result.Errors, error => error.Contains("Commit SHA", StringComparison.Ordinal));
        Assert.Contains(result.Errors, error => error.Contains("Version", StringComparison.Ordinal));
        Assert.Contains(result.Errors, error => error.Contains("Image digest", StringComparison.Ordinal));
        Assert.Contains(result.Errors, error => error.Contains("Unsupported capability", StringComparison.Ordinal));
    }

    [Fact]
    public void RegistrationTokenService_HashesAndPrefixesToken()
    {
        var service = new RegistrationTokenService();

        var token = service.CreateToken();

        Assert.StartsWith("dcr_", token.Secret, StringComparison.Ordinal);
        Assert.Equal(token.Secret[..16], token.Prefix);
        Assert.Equal(64, token.Hash.Length);
        Assert.Equal(token.Hash, service.HashToken(token.Secret));
        Assert.DoesNotContain(token.Secret, token.Hash, StringComparison.Ordinal);
    }

    [Fact]
    public void WorkflowSnippet_IncludesRegistrationCommandAndSecret()
    {
        var snippet = WorkflowSnippetBuilder.Build(new WorkflowSnippetContext(
            "https://devcontrol.example.com",
            "dcr_secret",
            "production",
            "$SERVICE_URL",
            "$HEALTH_URL",
            "${{ github.ref_name }}",
            "$IMAGE_DIGEST",
            "health,deployment-events"));

        Assert.Contains("DEVCONTROL_SERVER: https://devcontrol.example.com", snippet, StringComparison.Ordinal);
        Assert.Contains("DEVCONTROL_TOKEN: dcr_secret", snippet, StringComparison.Ordinal);
        Assert.Contains("devcontrol apps register", snippet, StringComparison.Ordinal);
        Assert.Contains("--environment production", snippet, StringComparison.Ordinal);
        Assert.Contains("--repo ${{ github.repository }}", snippet, StringComparison.Ordinal);
    }
}
