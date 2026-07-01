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
    string Capabilities);

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
        var block = BuildBlock(request);

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

    private static string BuildBlock(GitHubWorkflowOnboardingRequest request)
    {
        var serverUrl = EscapeShell(request.ServerUrl.TrimEnd('/'));
        var audience = EscapeShell(request.Audience);
        var environmentSlug = EscapeShell(request.EnvironmentSlug);
        var capabilities = EscapeShell(request.Capabilities);

        var builder = new StringBuilder();
        builder.AppendLine($"      {StartMarker}");
        builder.AppendLine("      - name: Install DevControl CLI");
        builder.AppendLine("        uses: fullstack-nick/DevControl/.github/actions/setup-devcontrol@main");
        builder.AppendLine();
        builder.AppendLine("      - name: Request DevControl OIDC token");
        builder.AppendLine("        uses: actions/github-script@v8");
        builder.AppendLine("        id: devcontrol_oidc");
        builder.AppendLine("        with:");
        builder.AppendLine("          script: |");
        builder.AppendLine("            const core = require('@actions/core')");
        builder.AppendLine($"            const token = await core.getIDToken('{audience}')");
        builder.AppendLine("            core.setSecret(token)");
        builder.AppendLine("            core.setOutput('token', token)");
        builder.AppendLine();
        builder.AppendLine("      - name: Register app in DevControl");
        builder.AppendLine("        env:");
        builder.AppendLine($"          DEVCONTROL_SERVER: {serverUrl}");
        builder.AppendLine("          DEVCONTROL_GITHUB_OIDC_TOKEN: ${{ steps.devcontrol_oidc.outputs.token }}");
        builder.AppendLine("        run: |");
        builder.AppendLine("          devcontrol apps register \\");
        builder.AppendLine($"            --environment {environmentSlug} \\");
        builder.AppendLine($"            --service-url \"{request.ServiceUrlExpression}\" \\");
        builder.AppendLine($"            --health-url \"{request.HealthUrlExpression}\" \\");
        builder.AppendLine("            --repo \"${{ github.repository }}\" \\");
        builder.AppendLine("            --commit-sha \"${{ github.sha }}\" \\");
        builder.AppendLine($"            --version \"{request.VersionExpression}\" \\");
        builder.AppendLine($"            --image-digest \"{request.ImageDigestExpression}\" \\");
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

    [GeneratedRegex("^  [A-Za-z0-9_.-]+:\\s*$")]
    private static partial Regex TopLevelJobRegex();

    [GeneratedRegex("^[A-Za-z0-9_.-]+$")]
    private static partial Regex JobIdRegex();

    [GeneratedRegex("^permissions:[ \\t]*\\S+", RegexOptions.Multiline)]
    private static partial Regex ScalarPermissionsRegex();
}
