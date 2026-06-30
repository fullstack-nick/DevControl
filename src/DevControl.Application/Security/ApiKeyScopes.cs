using System.Text.Json;

namespace DevControl.Application.Security;

public static class ApiKeyScopes
{
    public const string SampleRead = "sample:read";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly HashSet<string> Supported = new(StringComparer.Ordinal)
    {
        SampleRead
    };

    public static IReadOnlyList<string> SupportedScopes => [SampleRead];

    public static bool TryNormalize(
        IReadOnlyList<string>? requestedScopes,
        out IReadOnlyList<string> scopes,
        out string scopesJson,
        out IReadOnlyList<string> errors)
    {
        var requested = requestedScopes is null || requestedScopes.Count == 0
            ? [SampleRead]
            : requestedScopes;
        var normalized = requested
            .Select(scope => (scope ?? string.Empty).Trim())
            .Where(scope => scope.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();

        var validationErrors = normalized
            .Where(scope => !Supported.Contains(scope))
            .Select(scope => $"Unsupported API key scope '{scope}'.")
            .ToArray();

        scopes = normalized;
        scopesJson = JsonSerializer.Serialize(normalized, JsonOptions);
        errors = validationErrors;
        return validationErrors.Length == 0 && normalized.Length > 0;
    }

    public static IReadOnlyList<string> FromJson(string scopesJson)
    {
        try
        {
            return JsonSerializer.Deserialize<string[]>(scopesJson, JsonOptions) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }
}
