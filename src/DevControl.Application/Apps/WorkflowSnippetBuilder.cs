using System.Text;

namespace DevControl.Application.Apps;

public sealed record WorkflowSnippetContext(
    string ServerUrl,
    string TokenSecret,
    string EnvironmentSlug,
    string ServiceUrlPlaceholder,
    string HealthUrlPlaceholder,
    string VersionPlaceholder,
    string ImageDigestPlaceholder,
    string Capabilities);

public static class WorkflowSnippetBuilder
{
    public static string Build(WorkflowSnippetContext context)
    {
        var builder = new StringBuilder();
        builder.AppendLine("- name: Install DevControl CLI");
        builder.AppendLine("  uses: fullstack-nick/DevControl/.github/actions/setup-devcontrol@main");
        builder.AppendLine();
        builder.AppendLine("- name: Register app in DevControl");
        builder.AppendLine("  env:");
        builder.AppendLine($"    DEVCONTROL_SERVER: {context.ServerUrl}");
        builder.AppendLine($"    DEVCONTROL_TOKEN: {context.TokenSecret}");
        builder.AppendLine("  run: |");
        builder.AppendLine("    devcontrol apps register \\");
        builder.AppendLine($"      --environment {context.EnvironmentSlug} \\");
        builder.AppendLine($"      --service-url {context.ServiceUrlPlaceholder} \\");
        builder.AppendLine($"      --health-url {context.HealthUrlPlaceholder} \\");
        builder.AppendLine("      --repo ${{ github.repository }} \\");
        builder.AppendLine("      --commit-sha ${{ github.sha }} \\");
        builder.AppendLine($"      --version {context.VersionPlaceholder} \\");
        builder.AppendLine($"      --image-digest {context.ImageDigestPlaceholder} \\");
        builder.AppendLine($"      --capabilities {context.Capabilities} \\");
        builder.AppendLine("      --json");
        return builder.ToString();
    }
}
