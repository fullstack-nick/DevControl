using DevControl.Application.GitHub;
using DevControl.Domain.Enums;
using Xunit;

namespace DevControl.UnitTests;

public sealed class GitHubStage8Tests
{
    [Theory]
    [InlineData("fullstack-nick/devcontrol-sample-live-app", "fullstack-nick/devcontrol-sample-live-app")]
    [InlineData("https://github.com/fullstack-nick/devcontrol-sample-live-app", "fullstack-nick/devcontrol-sample-live-app")]
    [InlineData("https://github.com/fullstack-nick/devcontrol-sample-live-app.git", "fullstack-nick/devcontrol-sample-live-app")]
    public void RepoParser_AcceptsOwnerNameAndGitHubUrls(string value, string expected)
    {
        Assert.True(GitHubRepoNameParser.TryParse(value, out var repo));
        Assert.Equal(expected, repo.NormalizedFullName);
    }

    [Fact]
    public void OnboardingPatch_InsertsRegistrationBlockAndOidcPermission()
    {
        const string workflow = """
                                name: Deploy
                                on:
                                  push:
                                    branches: [main]
                                jobs:
                                  deploy:
                                    runs-on: ubuntu-latest
                                    steps:
                                      - uses: actions/checkout@v4
                                """;

        var result = GitHubWorkflowOnboardingPatchBuilder.Build(new GitHubWorkflowOnboardingRequest(
            workflow,
            "deploy",
            "https://devcontrol.example.com",
            "https://devcontrol.example.com/api/apps/register",
            "production",
            "${{ steps.deploy.outputs.service-url }}",
            "${{ steps.deploy.outputs.health-url }}",
            "${{ github.sha }}",
            "${{ steps.deploy.outputs.image-digest }}",
            "health,deployment-events,deploy"));

        Assert.True(result.Succeeded, result.Error);
        Assert.Contains("id-token: write", result.Content, StringComparison.Ordinal);
        Assert.Contains("# DEVCONTROL-REGISTRATION-START", result.Content, StringComparison.Ordinal);
        Assert.Contains("devcontrol apps register", result.Content, StringComparison.Ordinal);
        Assert.Contains("--github-oidc-token \"$DEVCONTROL_GITHUB_OIDC_TOKEN\"", result.Content, StringComparison.Ordinal);
        Assert.DoesNotContain("const core = require('@actions/core')", result.Content, StringComparison.Ordinal);
    }

    [Fact]
    public void OnboardingPatch_ReplacesExistingRegistrationBlock()
    {
        const string workflow = """
                                name: Deploy
                                on:
                                  workflow_dispatch:
                                permissions:
                                  contents: read
                                  id-token: write
                                jobs:
                                  deploy:
                                    runs-on: ubuntu-latest
                                    steps:
                                      # DEVCONTROL-REGISTRATION-START
                                      - name: Old
                                        run: echo old
                                      # DEVCONTROL-REGISTRATION-END
                                """;

        var result = GitHubWorkflowOnboardingPatchBuilder.Build(new GitHubWorkflowOnboardingRequest(
            workflow,
            "deploy",
            "https://devcontrol.example.com",
            "https://devcontrol.example.com/api/apps/register",
            "production",
            "https://sample.example.com",
            "https://sample.example.com/health",
            "v1",
            "sha256:test",
            "health"));

        Assert.True(result.Succeeded, result.Error);
        Assert.DoesNotContain("echo old", result.Content, StringComparison.Ordinal);
        Assert.Equal(1, CountOccurrences(result.Content, "# DEVCONTROL-REGISTRATION-START"));
    }

    [Fact]
    public void OnboardingPatch_ReplacesUnmarkedExistingDevControlRegistrationStep()
    {
        const string workflow = """
                                name: Deploy
                                on:
                                  workflow_dispatch:
                                permissions:
                                  contents: read
                                  id-token: write
                                jobs:
                                  deploy:
                                    runs-on: ubuntu-latest
                                    steps:
                                      - uses: actions/checkout@v4
                                      - name: Register in DevControl
                                        env:
                                          DEVCONTROL_TOKEN: ${{ secrets.DEVCONTROL_TOKEN }}
                                        run: |
                                          devcontrol apps register \
                                            --environment production \
                                            --service-url "$SERVICE_URL" \
                                            --health-url "$SERVICE_URL/health" \
                                            --capabilities health,deployment-events \
                                            --json
                                """;

        var result = GitHubWorkflowOnboardingPatchBuilder.Build(new GitHubWorkflowOnboardingRequest(
            workflow,
            "deploy",
            "https://devcontrol.example.com",
            "https://devcontrol.example.com/api/apps/register",
            "production",
            "$SERVICE_URL",
            "$SERVICE_URL/health",
            "${{ github.sha }}",
            "$IMAGE_DIGEST",
            "health,deployment-events,deploy"));

        Assert.True(result.Succeeded, result.Error);
        Assert.DoesNotContain("DEVCONTROL_TOKEN", result.Content, StringComparison.Ordinal);
        Assert.DoesNotContain("secrets.DEVCONTROL_TOKEN", result.Content, StringComparison.Ordinal);
        Assert.Equal(1, CountOccurrences(result.Content, "devcontrol apps register"));
        Assert.Contains("DEVCONTROL_GITHUB_OIDC_TOKEN", result.Content, StringComparison.Ordinal);
    }

    [Fact]
    public void OnboardingPatch_ReusesExistingDevControlSetupAction()
    {
        const string workflow = """
                                name: Deploy
                                on:
                                  workflow_dispatch:
                                permissions:
                                  contents: read
                                  id-token: write
                                jobs:
                                  deploy:
                                    runs-on: ubuntu-latest
                                    steps:
                                      - uses: actions/checkout@v4
                                      - name: Install DevControl CLI and pack SDK
                                        uses: fullstack-nick/DevControl/.github/actions/setup-devcontrol@main
                                        with:
                                          sdk-output: local_packages
                                      - name: Register in DevControl
                                        env:
                                          DEVCONTROL_TOKEN: ${{ secrets.DEVCONTROL_TOKEN }}
                                        run: |
                                          devcontrol apps register \
                                            --environment production \
                                            --service-url "$SERVICE_URL" \
                                            --health-url "$SERVICE_URL/health" \
                                            --capabilities health,deployment-events \
                                            --json
                                """;

        var result = GitHubWorkflowOnboardingPatchBuilder.Build(new GitHubWorkflowOnboardingRequest(
            workflow,
            "deploy",
            "https://devcontrol.example.com",
            "https://devcontrol.example.com/api/apps/register",
            "production",
            "$SERVICE_URL",
            "$SERVICE_URL/health",
            "${{ github.sha }}",
            "$IMAGE_DIGEST",
            "health,deployment-events,deploy"));

        Assert.True(result.Succeeded, result.Error);
        Assert.Equal(1, CountOccurrences(result.Content, "setup-devcontrol@main"));
        Assert.DoesNotContain("secrets.DEVCONTROL_TOKEN", result.Content, StringComparison.Ordinal);
        Assert.Contains("DEVCONTROL_GITHUB_OIDC_TOKEN", result.Content, StringComparison.Ordinal);
    }

    [Fact]
    public void OnboardingPatch_UsesConfiguredSetupActionReference()
    {
        const string workflow = """
                                name: Deploy
                                on:
                                  workflow_dispatch:
                                jobs:
                                  deploy:
                                    runs-on: ubuntu-latest
                                    steps:
                                      - uses: actions/checkout@v4
                                """;

        var result = GitHubWorkflowOnboardingPatchBuilder.Build(new GitHubWorkflowOnboardingRequest(
            workflow,
            "deploy",
            "https://devcontrol.example.com",
            "https://devcontrol.example.com/api/apps/register",
            "production",
            "$SERVICE_URL",
            "$SERVICE_URL/health",
            "${{ github.sha }}",
            "$IMAGE_DIGEST",
            "health,deployment-events,deploy",
            "acme/devcontrol-actions/.github/actions/setup-devcontrol@v1"));

        Assert.True(result.Succeeded, result.Error);
        Assert.Contains("uses: acme/devcontrol-actions/.github/actions/setup-devcontrol@v1", result.Content, StringComparison.Ordinal);
        Assert.DoesNotContain(DevControlSetupActionReference.Default, result.Content, StringComparison.Ordinal);
    }

    [Fact]
    public void OnboardingPatch_FailsWhenJobStepsCannotBeFound()
    {
        const string workflow = """
                                name: Deploy
                                on:
                                  workflow_dispatch:
                                jobs:
                                  build:
                                    runs-on: ubuntu-latest
                                    steps:
                                      - run: echo build
                                """;

        var result = GitHubWorkflowOnboardingPatchBuilder.Build(new GitHubWorkflowOnboardingRequest(
            workflow,
            "deploy",
            "https://devcontrol.example.com",
            "https://devcontrol.example.com/api/apps/register",
            "production",
            "https://sample.example.com",
            "https://sample.example.com/health",
            "v1",
            "sha256:test",
            "health"));

        Assert.False(result.Succeeded);
        Assert.Contains("Could not find a steps block", result.Error, StringComparison.Ordinal);
    }

    [Fact]
    public void OnboardingPatch_FailsOnScalarPermissions()
    {
        const string workflow = """
                                name: Deploy
                                on:
                                  workflow_dispatch:
                                permissions: read-all
                                jobs:
                                  deploy:
                                    runs-on: ubuntu-latest
                                    steps:
                                      - run: echo deploy
                                """;

        var result = GitHubWorkflowOnboardingPatchBuilder.Build(new GitHubWorkflowOnboardingRequest(
            workflow,
            "deploy",
            "https://devcontrol.example.com",
            "https://devcontrol.example.com/api/apps/register",
            "production",
            "https://sample.example.com",
            "https://sample.example.com/health",
            "v1",
            "sha256:test",
            "health"));

        Assert.False(result.Succeeded);
        Assert.Contains("scalar permissions", result.Error, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("queued", "", ControlActionStatus.InProgress)]
    [InlineData("completed", "success", ControlActionStatus.Succeeded)]
    [InlineData("completed", "cancelled", ControlActionStatus.Cancelled)]
    [InlineData("completed", "timed_out", ControlActionStatus.TimedOut)]
    [InlineData("completed", "failure", ControlActionStatus.Failed)]
    public void DispatchStatusMapper_MapsGitHubStatusToControlActionStatus(string status, string conclusion, ControlActionStatus expected)
    {
        Assert.Equal(expected, GitHubDispatchStatusMapper.ToControlActionStatus(status, conclusion));
    }

    private static int CountOccurrences(string value, string pattern)
    {
        var count = 0;
        var index = value.IndexOf(pattern, StringComparison.Ordinal);
        while (index >= 0)
        {
            count++;
            index = value.IndexOf(pattern, index + pattern.Length, StringComparison.Ordinal);
        }

        return count;
    }
}
