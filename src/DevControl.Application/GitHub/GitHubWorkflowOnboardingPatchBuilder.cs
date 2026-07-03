using System.Text;
using System.Text.RegularExpressions;

namespace DevControl.Application.GitHub;

public sealed record GitHubWorkflowOnboardingRequest(
    string WorkflowContent,
    string JobId,
    string ServerUrl,
    string Audience,
    string EnvironmentSlug,
    string ServiceUrlExpression,
    string HealthUrlExpression,
    string VersionExpression,
    string ImageDigestExpression,
    string Capabilities,
    string SetupActionReference = DevControlSetupActionReference.Default,
    Guid? RepoConnectionId = null);

public sealed record GitHubWorkflowOnboardingResult(bool Succeeded, string Content, string? Error)
{
    public static GitHubWorkflowOnboardingResult Success(string content) => new(true, content, null);

    public static GitHubWorkflowOnboardingResult Failure(string error) => new(false, string.Empty, error);
}

public static partial class GitHubWorkflowOnboardingPatchBuilder
{
    private const string StartMarker = "# DEVCONTROL-REGISTRATION-START";
    private const string EndMarker = "# DEVCONTROL-REGISTRATION-END";

    public static GitHubWorkflowOnboardingResult Build(GitHubWorkflowOnboardingRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.WorkflowContent))
        {
            return GitHubWorkflowOnboardingResult.Failure("Workflow content is empty.");
        }

        if (!JobIdRegex().IsMatch(request.JobId))
        {
            return GitHubWorkflowOnboardingResult.Failure("Job id can contain only letters, numbers, dot, underscore, or dash.");
        }

        var normalized = request.WorkflowContent.Replace("\r\n", "\n", StringComparison.Ordinal);
        if (ScalarPermissionsRegex().IsMatch(normalized))
        {
            return GitHubWorkflowOnboardingResult.Failure("Workflow uses scalar permissions; add an explicit permissions block before automated patching.");
        }

        normalized = EnsureIdTokenPermission(normalized);
        var setupActionReference = DevControlSetupActionReference.Normalize(request.SetupActionReference);
        var shouldInstallCli = !JobContainsSetupDevControl(normalized, request.JobId, setupActionReference);
        var block = BuildBlock(request, shouldInstallCli);

        var markerStart = normalized.IndexOf(StartMarker, StringComparison.Ordinal);
        if (markerStart >= 0)
        {
            var markerEnd = normalized.IndexOf(EndMarker, markerStart, StringComparison.Ordinal);
            if (markerEnd < 0)
            {
                return GitHubWorkflowOnboardingResult.Failure("Existing DevControl registration block is missing the end marker.");
            }

            markerStart = normalized.LastIndexOf('\n', markerStart);
            markerStart = markerStart < 0 ? 0 : markerStart + 1;
            markerEnd = normalized.IndexOf('\n', markerEnd);
            markerEnd = markerEnd < 0 ? normalized.Length : markerEnd + 1;
            var replaced = normalized[..markerStart] + block + normalized[markerEnd..];
            return GitHubWorkflowOnboardingResult.Success(replaced);
        }

        var existingRegistrationStep = FindExistingRegistrationStep(normalized, request.JobId);
        if (existingRegistrationStep is not null)
        {
            var replaced = normalized[..existingRegistrationStep.Start] + block + normalized[existingRegistrationStep.End..];
            return GitHubWorkflowOnboardingResult.Success(replaced);
        }

        var insertion = FindStepsInsertion(normalized, request.JobId);
        if (insertion is null)
        {
            return GitHubWorkflowOnboardingResult.Failure($"Could not find a steps block for job '{request.JobId}'.");
        }

        var next = normalized.Insert(insertion.Value, block);
        return GitHubWorkflowOnboardingResult.Success(next);
    }

    private static int? FindStepsInsertion(string content, string jobId)
    {
        var lines = content.Split('\n');
        var jobStart = -1;
        for (var i = 0; i < lines.Length; i++)
        {
            if (Regex.IsMatch(lines[i], $"^  {Regex.Escape(jobId)}:\\s*$"))
            {
                jobStart = i;
                break;
            }
        }

        if (jobStart < 0)
        {
            return null;
        }

        for (var i = jobStart + 1; i < lines.Length; i++)
        {
            if (TopLevelJobRegex().IsMatch(lines[i]))
            {
                return null;
            }

            if (Regex.IsMatch(lines[i], "^    steps:\\s*$"))
            {
                var offset = 0;
                for (var j = 0; j <= i; j++)
                {
                    offset += lines[j].Length + 1;
                }

                return offset;
            }
        }

        return null;
    }

    private static TextRange? FindExistingRegistrationStep(string content, string jobId)
    {
        var lines = content.Split('\n');
        var jobStart = FindJobStart(lines, jobId);
        if (jobStart < 0)
        {
            return null;
        }

        var stepsStart = -1;
        for (var i = jobStart + 1; i < lines.Length; i++)
        {
            if (TopLevelJobRegex().IsMatch(lines[i]))
            {
                return null;
            }

            if (Regex.IsMatch(lines[i], "^    steps:\\s*$"))
            {
                stepsStart = i;
                break;
            }
        }

        if (stepsStart < 0)
        {
            return null;
        }

        var currentStepStart = -1;
        for (var i = stepsStart + 1; i < lines.Length; i++)
        {
            if (TopLevelJobRegex().IsMatch(lines[i]))
            {
                return null;
            }

            if (Regex.IsMatch(lines[i], "^      -\\s"))
            {
                currentStepStart = i;
            }

            if (currentStepStart >= 0 && lines[i].Contains("devcontrol apps register", StringComparison.Ordinal))
            {
                var end = i + 1;
                while (end < lines.Length && !Regex.IsMatch(lines[end], "^      -\\s") && !TopLevelJobRegex().IsMatch(lines[end]))
                {
                    end++;
                }

                var endOffset = end >= lines.Length ? content.Length : LineOffset(lines, end);
                return new TextRange(LineOffset(lines, currentStepStart), endOffset);
            }
        }

        return null;
    }

    private static int FindJobStart(IReadOnlyList<string> lines, string jobId)
    {
        for (var i = 0; i < lines.Count; i++)
        {
            if (Regex.IsMatch(lines[i], $"^  {Regex.Escape(jobId)}:\\s*$"))
            {
                return i;
            }
        }

        return -1;
    }

    private static bool JobContainsSetupDevControl(string content, string jobId, string setupActionReference)
    {
        var lines = content.Split('\n');
        var jobStart = FindJobStart(lines, jobId);
        if (jobStart < 0)
        {
            return false;
        }

        for (var i = jobStart + 1; i < lines.Length; i++)
        {
            if (TopLevelJobRegex().IsMatch(lines[i]))
            {
                return false;
            }

            if (lines[i].Contains(setupActionReference, StringComparison.OrdinalIgnoreCase) ||
                lines[i].Contains(DevControlSetupActionReference.Default, StringComparison.OrdinalIgnoreCase) ||
                SetupDevControlActionRegex().IsMatch(lines[i]))
            {
                return true;
            }
        }

        return false;
    }

    private static int LineOffset(IReadOnlyList<string> lines, int lineIndex)
    {
        var offset = 0;
        for (var i = 0; i < lineIndex && i < lines.Count; i++)
        {
            offset += lines[i].Length + 1;
        }

        return offset;
    }

    private static string EnsureIdTokenPermission(string content)
    {
        var lines = content.Split('\n').ToList();
        var permissionsIndex = lines.FindIndex(line => Regex.IsMatch(line, "^permissions:\\s*$"));
        if (permissionsIndex < 0)
        {
            var insertAt = FindTopLevelInsertion(lines);
            lines.Insert(insertAt, "permissions:");
            lines.Insert(insertAt + 1, "  contents: read");
            lines.Insert(insertAt + 2, "  id-token: write");
            return string.Join('\n', lines);
        }

        var end = permissionsIndex + 1;
        while (end < lines.Count && (lines[end].StartsWith("  ", StringComparison.Ordinal) || string.IsNullOrWhiteSpace(lines[end])))
        {
            end++;
        }

        var idTokenIndex = -1;
        for (var i = permissionsIndex + 1; i < end; i++)
        {
            if (Regex.IsMatch(lines[i], "^\\s*id-token:\\s*\\S+\\s*$", RegexOptions.IgnoreCase))
            {
                idTokenIndex = i;
                break;
            }
        }

        if (idTokenIndex >= 0)
        {
            lines[idTokenIndex] = "  id-token: write";
        }
        else
        {
            lines.Insert(end, "  id-token: write");
        }

        return string.Join('\n', lines);
    }

    private static int FindTopLevelInsertion(IReadOnlyList<string> lines)
    {
        var onIndex = lines.ToList().FindIndex(line => Regex.IsMatch(line, "^on:\\s*$|^on:\\s*\\["));
        if (onIndex < 0)
        {
            return Math.Min(1, lines.Count);
        }

        var next = onIndex + 1;
        while (next < lines.Count && (lines[next].StartsWith("  ", StringComparison.Ordinal) || string.IsNullOrWhiteSpace(lines[next])))
        {
            next++;
        }

        return next;
    }

    private static string BuildBlock(GitHubWorkflowOnboardingRequest request, bool includeCliInstall)
    {
        var serverUrl = EscapeShell(request.ServerUrl.TrimEnd('/'));
        var audience = EscapeShell(request.Audience);
        var environmentSlug = EscapeShell(request.EnvironmentSlug);
        var capabilities = EscapeShell(request.Capabilities);
        var serviceUrlExpression = EscapeBashDoubleQuoted(request.ServiceUrlExpression);
        var healthUrlExpression = EscapeBashDoubleQuoted(request.HealthUrlExpression);
        var versionExpression = EscapeBashDoubleQuoted(request.VersionExpression);
        var imageDigestExpression = EscapeBashDoubleQuoted(request.ImageDigestExpression);

        var builder = new StringBuilder();
        builder.AppendLine($"      {StartMarker}");
        if (includeCliInstall)
        {
            builder.AppendLine("      - name: Install DevControl CLI");
            builder.AppendLine($"        uses: {DevControlSetupActionReference.Normalize(request.SetupActionReference)}");
            builder.AppendLine();
        }

        builder.AppendLine("      - name: Request DevControl OIDC token");
        builder.AppendLine("        uses: actions/github-script@v8");
        builder.AppendLine("        id: devcontrol_oidc");
        builder.AppendLine("        with:");
        builder.AppendLine("          script: |");
        builder.AppendLine($"            const token = await core.getIDToken('{audience}')");
        builder.AppendLine("            core.setSecret(token)");
        builder.AppendLine("            core.setOutput('token', token)");
        builder.AppendLine();
        builder.AppendLine("      - name: Register app in DevControl");
        builder.AppendLine("        env:");
        builder.AppendLine($"          DEVCONTROL_SERVER: {serverUrl}");
        builder.AppendLine("          DEVCONTROL_GITHUB_OIDC_TOKEN: ${{ steps.devcontrol_oidc.outputs.token }}");
        builder.AppendLine("        run: |");
        builder.AppendLine($"          DEVCONTROL_REGISTER_SERVICE_URL=\"{serviceUrlExpression}\"");
        builder.AppendLine($"          DEVCONTROL_REGISTER_HEALTH_URL=\"{healthUrlExpression}\"");
        builder.AppendLine($"          DEVCONTROL_REGISTER_VERSION=\"{versionExpression}\"");
        builder.AppendLine($"          DEVCONTROL_REGISTER_IMAGE_DIGEST=\"{imageDigestExpression}\"");
        builder.AppendLine("          for name in DEVCONTROL_REGISTER_SERVICE_URL DEVCONTROL_REGISTER_HEALTH_URL DEVCONTROL_REGISTER_VERSION DEVCONTROL_REGISTER_IMAGE_DIGEST; do");
        builder.AppendLine("            if [ -z \"${!name}\" ]; then");
        builder.AppendLine("              echo \"::error::$name is empty. Update the DevControl onboarding expression to a literal, workflow output, or shell variable produced earlier in this job.\"");
        builder.AppendLine("              exit 1");
        builder.AppendLine("            fi");
        builder.AppendLine("          done");
        builder.AppendLine("          devcontrol apps register \\");
        builder.AppendLine($"            --environment {environmentSlug} \\");
        if (request.RepoConnectionId is { } repoConnectionId)
        {
            builder.AppendLine($"            --repo-connection-id \"{repoConnectionId}\" \\");
        }

        builder.AppendLine("            --service-url \"$DEVCONTROL_REGISTER_SERVICE_URL\" \\");
        builder.AppendLine("            --health-url \"$DEVCONTROL_REGISTER_HEALTH_URL\" \\");
        builder.AppendLine("            --repo \"${{ github.repository }}\" \\");
        builder.AppendLine("            --commit-sha \"${{ github.sha }}\" \\");
        builder.AppendLine("            --version \"$DEVCONTROL_REGISTER_VERSION\" \\");
        builder.AppendLine("            --image-digest \"$DEVCONTROL_REGISTER_IMAGE_DIGEST\" \\");
        builder.AppendLine($"            --capabilities {capabilities} \\");
        builder.AppendLine("            --github-oidc-token \"$DEVCONTROL_GITHUB_OIDC_TOKEN\" \\");
        builder.AppendLine("            --json");
        builder.AppendLine($"      {EndMarker}");
        return builder.ToString();
    }

    private static string EscapeShell(string value)
    {
        return value.Replace("'", "'\"'\"'", StringComparison.Ordinal);
    }

    private static string EscapeBashDoubleQuoted(string value)
    {
        return value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal)
            .Replace("`", "\\`", StringComparison.Ordinal)
            .Replace("\r", string.Empty, StringComparison.Ordinal)
            .Replace("\n", string.Empty, StringComparison.Ordinal);
    }

    [GeneratedRegex("^  [A-Za-z0-9_.-]+:\\s*$")]
    private static partial Regex TopLevelJobRegex();

    [GeneratedRegex("^[A-Za-z0-9_.-]+$")]
    private static partial Regex JobIdRegex();

    [GeneratedRegex("^permissions:[ \\t]*\\S+", RegexOptions.Multiline)]
    private static partial Regex ScalarPermissionsRegex();

    [GeneratedRegex("uses:\\s*\\S+/\\.github/actions/setup-devcontrol@\\S+", RegexOptions.IgnoreCase)]
    private static partial Regex SetupDevControlActionRegex();

    private sealed record TextRange(int Start, int End);
}
