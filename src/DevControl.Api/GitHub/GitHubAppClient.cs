using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using DevControl.Application.GitHub;

namespace DevControl.Api.GitHub;

public sealed class GitHubAppClient(HttpClient httpClient, GitHubAppOptions options, TimeProvider timeProvider) : IGitHubAppClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public bool IsConfigured => options.IsConfigured;

    public async Task<GitHubInstallationInfo?> GetRepositoryInstallationAsync(GitHubRepoName repo, CancellationToken cancellationToken)
    {
        EnsureConfigured();
        using var request = CreateRequest(HttpMethod.Get, $"repos/{repo.Owner}/{repo.Name}/installation", CreateAppJwt());
        using var response = await httpClient.SendAsync(request, cancellationToken);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }

        await EnsureSuccessAsync(response, cancellationToken);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        var root = document.RootElement;
        var account = root.GetProperty("account");
        return new GitHubInstallationInfo(
            root.GetProperty("id").GetInt64(),
            account.GetProperty("login").GetString() ?? repo.Owner,
            account.GetProperty("type").GetString() ?? string.Empty,
            root.GetProperty("repository_selection").GetString() ?? string.Empty,
            JsonSerializer.Serialize(root.GetProperty("permissions"), JsonOptions));
    }

    public async Task<GitHubRepositoryInfo> GetRepositoryAsync(GitHubRepoName repo, long installationId, CancellationToken cancellationToken)
    {
        var token = await CreateInstallationTokenAsync(installationId, new { metadata = "read", contents = "read" }, cancellationToken);
        using var request = CreateRequest(HttpMethod.Get, $"repos/{repo.Owner}/{repo.Name}", token);
        using var response = await httpClient.SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        var root = document.RootElement;
        return new GitHubRepositoryInfo(
            root.GetProperty("full_name").GetString() ?? repo.FullName,
            root.GetProperty("default_branch").GetString() ?? "main",
            root.GetProperty("html_url").GetString() ?? $"https://github.com/{repo.FullName}");
    }

    public async Task<IReadOnlyList<GitHubWorkflowInfo>> ListWorkflowsAsync(GitHubRepoName repo, long installationId, CancellationToken cancellationToken)
    {
        var token = await CreateInstallationTokenAsync(installationId, new { actions = "read" }, cancellationToken);
        using var request = CreateRequest(HttpMethod.Get, $"repos/{repo.Owner}/{repo.Name}/actions/workflows?per_page=100", token);
        using var response = await httpClient.SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        var workflows = new List<GitHubWorkflowInfo>();
        foreach (var workflow in document.RootElement.GetProperty("workflows").EnumerateArray())
        {
            workflows.Add(new GitHubWorkflowInfo(
                workflow.GetProperty("id").GetInt64(),
                workflow.GetProperty("name").GetString() ?? string.Empty,
                workflow.GetProperty("path").GetString() ?? string.Empty,
                workflow.GetProperty("state").GetString() ?? string.Empty));
        }

        return workflows;
    }

    public async Task<GitHubFileContent> GetFileContentAsync(GitHubRepoName repo, long installationId, string path, string gitRef, CancellationToken cancellationToken)
    {
        var token = await CreateInstallationTokenAsync(installationId, new { contents = "read" }, cancellationToken);
        using var request = CreateRequest(HttpMethod.Get, $"repos/{repo.Owner}/{repo.Name}/contents/{EncodePath(path)}?ref={Uri.EscapeDataString(gitRef)}", token);
        using var response = await httpClient.SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        var root = document.RootElement;
        var encoded = root.GetProperty("content").GetString() ?? string.Empty;
        var bytes = Convert.FromBase64String(encoded.Replace("\n", string.Empty, StringComparison.Ordinal));
        return new GitHubFileContent(
            root.GetProperty("path").GetString() ?? path,
            root.GetProperty("sha").GetString() ?? string.Empty,
            Encoding.UTF8.GetString(bytes));
    }

    public async Task<GitHubPullRequestInfo> CreateOnboardingPullRequestAsync(
        GitHubRepoName repo,
        long installationId,
        string baseBranch,
        string headBranch,
        string workflowPath,
        string currentFileSha,
        string patchedContent,
        string title,
        string body,
        CancellationToken cancellationToken)
    {
        var token = await CreateInstallationTokenAsync(installationId, new { contents = "write", workflows = "write", pull_requests = "write" }, cancellationToken);
        var baseSha = await GetBranchShaAsync(repo, token, baseBranch, cancellationToken);

        using (var createRef = CreateRequest(HttpMethod.Post, $"repos/{repo.Owner}/{repo.Name}/git/refs", token))
        {
            createRef.Content = JsonContent(new { @ref = $"refs/heads/{headBranch}", sha = baseSha });
            using var refResponse = await httpClient.SendAsync(createRef, cancellationToken);
            await EnsureSuccessAsync(refResponse, cancellationToken);
        }

        using (var updateFile = CreateRequest(HttpMethod.Put, $"repos/{repo.Owner}/{repo.Name}/contents/{EncodePath(workflowPath)}", token))
        {
            updateFile.Content = JsonContent(new
            {
                message = "Add DevControl registration hook",
                content = Convert.ToBase64String(Encoding.UTF8.GetBytes(patchedContent)),
                sha = currentFileSha,
                branch = headBranch
            });
            using var fileResponse = await httpClient.SendAsync(updateFile, cancellationToken);
            await EnsureSuccessAsync(fileResponse, cancellationToken);
        }

        using var pr = CreateRequest(HttpMethod.Post, $"repos/{repo.Owner}/{repo.Name}/pulls", token);
        pr.Content = JsonContent(new
        {
            title,
            head = headBranch,
            @base = baseBranch,
            body,
            draft = false,
            maintainer_can_modify = true
        });
        using var prResponse = await httpClient.SendAsync(pr, cancellationToken);
        await EnsureSuccessAsync(prResponse, cancellationToken);
        using var document = JsonDocument.Parse(await prResponse.Content.ReadAsStringAsync(cancellationToken));
        var root = document.RootElement;
        return new GitHubPullRequestInfo(root.GetProperty("number").GetInt32(), root.GetProperty("html_url").GetString() ?? string.Empty);
    }

    public async Task<GitHubPullRequestState?> GetPullRequestAsync(GitHubRepoName repo, long installationId, int pullRequestNumber, CancellationToken cancellationToken)
    {
        var token = await CreateInstallationTokenAsync(installationId, new { pull_requests = "read" }, cancellationToken);
        using var request = CreateRequest(HttpMethod.Get, $"repos/{repo.Owner}/{repo.Name}/pulls/{pullRequestNumber}", token);
        using var response = await httpClient.SendAsync(request, cancellationToken);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }

        await EnsureSuccessAsync(response, cancellationToken);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        var root = document.RootElement;
        var mergedAt = ReadNullableDate(root, "merged_at");
        var closedAt = ReadNullableDate(root, "closed_at");
        return new GitHubPullRequestState(
            root.GetProperty("state").GetString() ?? "open",
            root.TryGetProperty("merged", out var merged) && merged.ValueKind == JsonValueKind.True,
            mergedAt,
            closedAt);
    }

    public async Task<GitHubWorkflowDispatchInfo> DispatchWorkflowAsync(
        GitHubRepoName repo,
        long installationId,
        string workflowPath,
        string gitRef,
        IReadOnlyDictionary<string, string> inputs,
        DateTimeOffset requestedAt,
        CancellationToken cancellationToken)
    {
        var token = await CreateInstallationTokenAsync(installationId, new { actions = "write" }, cancellationToken);
        using var request = CreateRequest(HttpMethod.Post, $"repos/{repo.Owner}/{repo.Name}/actions/workflows/{Uri.EscapeDataString(workflowPath)}/dispatches", token);
        request.Content = JsonContent(new { @ref = gitRef, inputs });
        using var response = await httpClient.SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);

        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!string.IsNullOrWhiteSpace(content))
        {
            using var document = JsonDocument.Parse(content);
            var root = document.RootElement;
            if (root.TryGetProperty("workflow_run_id", out var runId))
            {
                return new GitHubWorkflowDispatchInfo(runId.GetInt64(), root.GetProperty("html_url").GetString() ?? string.Empty);
            }
        }

        return new GitHubWorkflowDispatchInfo(null, string.Empty);
    }

    public async Task<GitHubWorkflowRunInfo?> GetWorkflowRunAsync(GitHubRepoName repo, long installationId, long runId, CancellationToken cancellationToken)
    {
        var token = await CreateInstallationTokenAsync(installationId, new { actions = "read" }, cancellationToken);
        using var request = CreateRequest(HttpMethod.Get, $"repos/{repo.Owner}/{repo.Name}/actions/runs/{runId}", token);
        using var response = await httpClient.SendAsync(request, cancellationToken);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }

        await EnsureSuccessAsync(response, cancellationToken);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        var root = document.RootElement;
        return ToWorkflowRunInfo(root);
    }

    public async Task<GitHubWorkflowRunInfo?> FindWorkflowRunAsync(
        GitHubRepoName repo,
        long installationId,
        string workflowPath,
        string gitRef,
        DateTimeOffset requestedAt,
        CancellationToken cancellationToken)
    {
        var token = await CreateInstallationTokenAsync(installationId, new { actions = "read" }, cancellationToken);
        var branch = gitRef.StartsWith("refs/heads/", StringComparison.Ordinal)
            ? gitRef["refs/heads/".Length..]
            : gitRef;
        var created = Uri.EscapeDataString($">={requestedAt.AddMinutes(-2).UtcDateTime:yyyy-MM-ddTHH:mm:ssZ}");
        var path = $"repos/{repo.Owner}/{repo.Name}/actions/workflows/{Uri.EscapeDataString(workflowPath)}/runs?event=workflow_dispatch&branch={Uri.EscapeDataString(branch)}&created={created}&per_page=10";
        using var request = CreateRequest(HttpMethod.Get, path, token);
        using var response = await httpClient.SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        if (!document.RootElement.TryGetProperty("workflow_runs", out var runs))
        {
            return null;
        }

        foreach (var run in runs.EnumerateArray())
        {
            if (string.Equals(run.GetProperty("event").GetString(), "workflow_dispatch", StringComparison.Ordinal))
            {
                return ToWorkflowRunInfo(run);
            }
        }

        return null;
    }

    private async Task<string> GetBranchShaAsync(GitHubRepoName repo, string token, string branch, CancellationToken cancellationToken)
    {
        using var request = CreateRequest(HttpMethod.Get, $"repos/{repo.Owner}/{repo.Name}/git/ref/heads/{Uri.EscapeDataString(branch)}", token);
        using var response = await httpClient.SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        return document.RootElement.GetProperty("object").GetProperty("sha").GetString() ?? throw new InvalidOperationException("GitHub branch response did not include a SHA.");
    }

    private async Task<string> CreateInstallationTokenAsync(long installationId, object permissions, CancellationToken cancellationToken)
    {
        using var request = CreateRequest(HttpMethod.Post, $"app/installations/{installationId}/access_tokens", CreateAppJwt());
        request.Content = JsonContent(new { permissions });
        using var response = await httpClient.SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        return document.RootElement.GetProperty("token").GetString() ?? throw new InvalidOperationException("GitHub installation token response did not include a token.");
    }

    private HttpRequestMessage CreateRequest(HttpMethod method, string path, string token)
    {
        var request = new HttpRequestMessage(method, new Uri(new Uri(options.ApiBaseUrl.TrimEnd('/') + "/"), path));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        request.Headers.UserAgent.ParseAdd("DevControl/1.0");
        request.Headers.Add("X-GitHub-Api-Version", "2022-11-28");
        return request;
    }

    private string CreateAppJwt()
    {
        EnsureConfigured();
        using var rsa = RSA.Create();
        rsa.ImportFromPem(options.PrivateKey);
        var now = timeProvider.GetUtcNow();
        var header = Base64Url(JsonSerializer.SerializeToUtf8Bytes(new { alg = "RS256", typ = "JWT" }));
        var payload = Base64Url(JsonSerializer.SerializeToUtf8Bytes(new
        {
            iat = now.AddSeconds(-60).ToUnixTimeSeconds(),
            exp = now.AddMinutes(9).ToUnixTimeSeconds(),
            iss = options.AppId
        }));
        var signingInput = $"{header}.{payload}";
        var signature = rsa.SignData(Encoding.ASCII.GetBytes(signingInput), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        return $"{signingInput}.{Base64Url(signature)}";
    }

    private void EnsureConfigured()
    {
        if (!IsConfigured)
        {
            throw new InvalidOperationException("GitHub App is not configured.");
        }
    }

    private static string Base64Url(byte[] bytes)
    {
        return Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    private static HttpContent JsonContent(object value)
    {
        return new StringContent(JsonSerializer.Serialize(value, JsonOptions), Encoding.UTF8, "application/json");
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        throw new HttpRequestException($"GitHub API request failed with {(int)response.StatusCode}: {body}");
    }

    private static string EncodePath(string path)
    {
        return string.Join("/", path.Split('/', StringSplitOptions.RemoveEmptyEntries).Select(Uri.EscapeDataString));
    }

    private static DateTimeOffset? ReadNullableDate(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out var property) || property.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        return property.GetDateTimeOffset();
    }

    private static GitHubWorkflowRunInfo ToWorkflowRunInfo(JsonElement root)
    {
        return new GitHubWorkflowRunInfo(
            root.GetProperty("id").GetInt64(),
            root.GetProperty("html_url").GetString() ?? string.Empty,
            root.GetProperty("status").GetString() ?? string.Empty,
            root.TryGetProperty("conclusion", out var conclusion) && conclusion.ValueKind != JsonValueKind.Null ? conclusion.GetString() ?? string.Empty : string.Empty,
            ReadNullableDate(root, "updated_at"));
    }
}
