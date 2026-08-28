using System.Net.Http.Headers;
using System.Text.Json;
using AgentDisplay.Contracts;
using Microsoft.Extensions.Options;

namespace AgentDisplay.Host;

public sealed class ClaudeUsageCollector(HttpClient http, IOptionsMonitor<AgentDisplayOptions> options, RuntimeSettingsService runtime, DirectoryLocator directories, ILogger<ClaudeUsageCollector> logger)
{
    private IReadOnlyList<UsageWindow>? cachedWindows;
    private DateTimeOffset nextRefreshAt;

    public async Task<ProviderUsage?> TryCollectAsync(TokenUsage observed, decimal estimatedCost, CancellationToken cancellationToken)
    {
        if (!(runtime.Snapshot().EnableClaudeUsage ?? options.CurrentValue.EnableClaudeUsage)) return null;
        var now = DateTimeOffset.UtcNow;
        if (now < nextRefreshAt) return cachedWindows is null ? Unavailable(observed, estimatedCost) : FromCache(observed, estimatedCost);
        nextRefreshAt = now.AddMinutes(5);
        var token = await ReadTokenAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(token)) return null;

        using var request = new HttpRequestMessage(HttpMethod.Get, "https://api.anthropic.com/api/oauth/usage");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Headers.TryAddWithoutValidation("anthropic-beta", "oauth-2025-04-20");
        request.Headers.UserAgent.ParseAdd("AgentDisplay/0.1");
        try
        {
            using var response = await http.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("Claude usage endpoint returned {StatusCode}", response.StatusCode);
                return cachedWindows is null ? Unavailable(observed, estimatedCost) : FromCache(observed, estimatedCost);
            }
            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            var windows = new List<UsageWindow>();
            AddWindow(doc.RootElement, windows, "five_hour", "5h", "5 hour");
            AddWindow(doc.RootElement, windows, "seven_day", "7d", "7 day");
            AddWindow(doc.RootElement, windows, "seven_day_sonnet", "7d-sonnet", "7 day Sonnet");
            AddWindow(doc.RootElement, windows, "seven_day_opus", "7d-opus", "7 day Opus");
            // Anthropic currently reports extra-usage credits in cents. Keep the
            // conversion at the provider boundary so the rest of the product is
            // consistently denominated in USD.
            var overageUsd = ReadDecimal(doc.RootElement, "extra_usage", "used_credits") / 100m;
            if (overageUsd is > 0 && windows.Count > 0)
            {
                windows[0] = windows[0] with { OverageUsd = overageUsd };
            }
            cachedWindows = windows;
            return FromCache(observed, estimatedCost);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            logger.LogWarning(ex, "Claude usage refresh failed");
            return cachedWindows is null ? Unavailable(observed, estimatedCost) : FromCache(observed, estimatedCost);
        }
    }

    private ProviderUsage FromCache(TokenUsage observed, decimal estimatedCost) =>
        new(AgentProvider.Claude, true, "Claude reported limits", cachedWindows ?? [], observed, estimatedCost, MetricSource.Provider, DateTimeOffset.UtcNow);

    private static ProviderUsage Unavailable(TokenUsage observed, decimal estimatedCost) =>
        new(AgentProvider.Claude, true, "Claude limits temporarily unavailable", [], observed, estimatedCost, MetricSource.Provider, DateTimeOffset.UtcNow);

    private async Task<string?> ReadTokenAsync(CancellationToken cancellationToken)
    {
        var env = Environment.GetEnvironmentVariable("CLAUDE_CODE_OAUTH_TOKEN");
        if (!string.IsNullOrWhiteSpace(env)) return env.Trim();
        var path = Path.Combine(directories.Root(AgentProvider.Claude), ".credentials.json");
        if (!File.Exists(path)) return null;
        try
        {
            await using var stream = File.OpenRead(path);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            if (!TryFind(doc.RootElement, "claudeAiOauth", out var oauth) || oauth.ValueKind != JsonValueKind.Object) return null;
            return FindDirectString(oauth, "accessToken") ?? FindDirectString(oauth, "access_token");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            logger.LogWarning(ex, "Unable to read Claude credentials");
            return null;
        }
    }

    private static string? FindDirectString(JsonElement root, string name)
    {
        foreach (var property in root.EnumerateObject())
            if (property.Name.Equals(name, StringComparison.OrdinalIgnoreCase) && property.Value.ValueKind == JsonValueKind.String)
                return property.Value.GetString();
        return null;
    }

    private static void AddWindow(JsonElement root, ICollection<UsageWindow> windows, string objectName, string key, string label)
    {
        if (!TryFind(root, objectName, out var item) || item.ValueKind != JsonValueKind.Object) return;
        var utilization = ReadNumber(item, "utilization");
        if (utilization is null) return;
        var percent = utilization <= 1 ? utilization.Value * 100 : utilization.Value;
        var resetText = FindString(item, "resets_at") ?? FindString(item, "resetsAt");
        DateTimeOffset? reset = DateTimeOffset.TryParse(resetText, out var parsed) ? parsed : null;
        windows.Add(new UsageWindow(key, label, Math.Clamp(percent, 0, 100), reset, null, MetricSource.Provider));
    }

    private static decimal? ReadDecimal(JsonElement root, string objectName, string property)
    {
        if (!TryFind(root, objectName, out var item)) return null;
        if (TryFind(item, property, out var value) && value.ValueKind == JsonValueKind.Number && value.TryGetDecimal(out var number)) return number;
        return null;
    }

    private static double? ReadNumber(JsonElement root, string name)
    {
        if (TryFind(root, name, out var value) && value.TryGetDouble(out var number)) return number;
        return null;
    }

    private static string? FindString(JsonElement root, string name)
    {
        if (TryFind(root, name, out var value) && value.ValueKind == JsonValueKind.String) return value.GetString();
        return null;
    }

    private static bool TryFind(JsonElement root, string name, out JsonElement value)
    {
        if (root.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in root.EnumerateObject())
            {
                if (property.Name.Equals(name, StringComparison.OrdinalIgnoreCase)) { value = property.Value; return true; }
                if (TryFind(property.Value, name, out value)) return true;
            }
        }
        else if (root.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in root.EnumerateArray()) if (TryFind(item, name, out value)) return true;
        }
        value = default;
        return false;
    }
}
