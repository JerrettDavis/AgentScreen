using System.Text.Json;
using AgentDisplay.Contracts;

namespace AgentDisplay.Host;

public sealed class CodexUsageCollector(DirectoryLocator directories, ILogger<CodexUsageCollector> logger)
{
    public async Task<ProviderUsage?> TryCollectAsync(TokenUsage observed, decimal estimatedCost, CancellationToken cancellationToken)
    {
        var sessionsRoot = Path.Combine(directories.Root(AgentProvider.Codex), "sessions");
        if (!Directory.Exists(sessionsRoot)) return null;

        string[] files;
        try
        {
            files = Directory.EnumerateFiles(sessionsRoot, "*.jsonl", SearchOption.AllDirectories)
                .OrderByDescending(SafeWriteTime)
                .Take(20)
                .ToArray();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            logger.LogDebug(ex, "Unable to enumerate Codex usage events");
            return null;
        }

        foreach (var file in files)
        {
            try
            {
                var lines = await TailReader.ReadLinesAsync(file, 8 * 1024 * 1024, cancellationToken);
                foreach (var line in lines.Reverse())
                {
                    if (!line.Contains("\"rate_limits\"", StringComparison.Ordinal)) continue;
                    using var doc = JsonDocument.Parse(line);
                    if (!TryRateLimits(doc.RootElement, out var limits)) continue;
                    var windows = ReadWindows(limits);
                    if (windows.Count == 0) continue;
                    return new ProviderUsage(AgentProvider.Codex, true, "Codex reported limits", windows, observed, estimatedCost, MetricSource.Provider, DateTimeOffset.UtcNow);
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
            {
                logger.LogDebug(ex, "Skipped Codex usage events in {Path}", file);
            }
        }
        return null;
    }

    private static List<UsageWindow> ReadWindows(JsonElement limits)
    {
        var windows = new List<UsageWindow>();
        AddWindow(limits, windows, "primary");
        AddWindow(limits, windows, "secondary");
        return windows.OrderBy(x => WindowMinutes(x.Key)).ToList();
    }

    private static void AddWindow(JsonElement limits, ICollection<UsageWindow> windows, string name)
    {
        if (!limits.TryGetProperty(name, out var item) || item.ValueKind != JsonValueKind.Object) return;
        if (!item.TryGetProperty("used_percent", out var used) || !used.TryGetDouble(out var percent)) return;
        if (!item.TryGetProperty("window_minutes", out var duration) || !duration.TryGetInt32(out var minutes)) return;
        DateTimeOffset? reset = null;
        if (item.TryGetProperty("resets_at", out var resetValue) && resetValue.TryGetInt64(out var epoch))
            reset = DateTimeOffset.FromUnixTimeSeconds(epoch);
        windows.Add(new UsageWindow($"{minutes}m", WindowLabel(minutes), Math.Clamp(percent, 0, 100), reset, null, MetricSource.Provider, "Reported by Codex"));
    }

    private static string WindowLabel(int minutes) => minutes switch
    {
        300 => "5 hour",
        10_080 => "7 day",
        _ when minutes % 1_440 == 0 => $"{minutes / 1_440} day",
        _ when minutes % 60 == 0 => $"{minutes / 60} hour",
        _ => $"{minutes} minute"
    };

    private static int WindowMinutes(string key) => int.TryParse(key.TrimEnd('m'), out var value) ? value : int.MaxValue;
    private static DateTime SafeWriteTime(string path) { try { return File.GetLastWriteTimeUtc(path); } catch { return DateTime.MinValue; } }

    private static bool TryRateLimits(JsonElement root, out JsonElement limits)
    {
        if (root.TryGetProperty("payload", out var payload) && payload.ValueKind == JsonValueKind.Object &&
            payload.TryGetProperty("rate_limits", out limits) && limits.ValueKind == JsonValueKind.Object) return true;
        limits = default;
        return false;
    }
}
