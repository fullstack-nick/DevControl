using System.Text.Json;

namespace DevControl.Application.Webhooks;

public static class WebhookEventTypes
{
    public const string Test = "webhook.test";
    public const string AppRegistered = "app.registered";
    public const string ApiKeyCreated = "api_key.created";
    public const string ApiKeyRevoked = "api_key.revoked";
    public const string ApiKeyRotated = "api_key.rotated";
    public const string FeatureFlagCreated = "feature_flag.created";
    public const string FeatureFlagUpdated = "feature_flag.updated";

    private static readonly string[] Ordered =
    [
        Test,
        AppRegistered,
        ApiKeyCreated,
        ApiKeyRevoked,
        ApiKeyRotated,
        FeatureFlagCreated,
        FeatureFlagUpdated
    ];

    private static readonly HashSet<string> Allowed = new(Ordered, StringComparer.Ordinal);

    public static IReadOnlyList<string> All => Ordered;

    public static bool TryNormalize(IReadOnlyList<string>? requested, out IReadOnlyList<string> eventTypes, out IReadOnlyList<string> errors)
    {
        var normalized = new SortedSet<string>(StringComparer.Ordinal);
        var validationErrors = new List<string>();

        foreach (var eventType in requested ?? [])
        {
            var value = eventType.Trim();
            if (!Allowed.Contains(value))
            {
                validationErrors.Add($"Unsupported webhook event type '{value}'.");
                continue;
            }

            normalized.Add(value);
        }

        if (normalized.Count == 0)
        {
            validationErrors.Add("At least one webhook event type is required.");
        }

        eventTypes = Ordered.Where(normalized.Contains).ToArray();
        errors = validationErrors;
        return validationErrors.Count == 0;
    }

    public static string ToJson(IReadOnlyList<string> eventTypes)
    {
        return JsonSerializer.Serialize(eventTypes, new JsonSerializerOptions(JsonSerializerDefaults.Web));
    }

    public static IReadOnlyList<string> FromJson(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<string[]>(json, new JsonSerializerOptions(JsonSerializerDefaults.Web)) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }
}
