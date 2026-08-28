using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AgentDisplay.Contracts;

namespace AgentDisplay.Core;

public sealed class AgentLogParser(PricingCatalog pricing, Redactor redactor)
{
    public AgentSession? Parse(AgentProvider provider, string filePath, IEnumerable<string> lines)
    {
        var turns = new List<AgentTurn>();
        string? sessionId = null;
        string? cwd = null;
        string? model = null;
        string? agent = null;
        DateTimeOffset? first = null;
        DateTimeOffset? last = null;

        foreach (var line in lines.Where(x => !string.IsNullOrWhiteSpace(x)).TakeLast(2_000))
        {
            JsonDocument? document = null;
            try
            {
                document = JsonDocument.Parse(line);
                var root = document.RootElement;
                sessionId ??= FirstString(root, "session_id", "sessionId", "conversation_id", "conversationId");
                cwd ??= FirstString(root, "cwd", "working_directory", "workingDirectory", "workspace", "project_path");
                model ??= FirstString(root, "model", "model_name", "modelName");
                agent ??= FirstString(root, "agent", "agent_name", "agentName", "subagent_type", "subagentType");

                var at = FirstDate(root, "timestamp", "created_at", "createdAt", "time", "at") ?? DateTimeOffset.UtcNow;
                first = first is null || at < first ? at : first;
                last = last is null || at > last ? at : last;

                var kind = InferKind(root);
                var summary = redactor.Text(ExtractSummary(root), 220);
                var tool = FirstString(root, "tool_name", "toolName", "name");
                var tokens = ExtractTokens(root);
                if (summary.Length == 0 && tool is null && tokens.Total == 0)
                {
                    continue;
                }

                var cost = pricing.Estimate(tokens, model, provider);
                turns.Add(new AgentTurn(
                    Id: StableId(line),
                    At: at,
                    Kind: kind,
                    Summary: summary.Length == 0 ? tool ?? "Agent activity" : summary,
                    ToolName: kind == TurnKind.Tool ? tool : null,
                    Decision: null,
                    Tokens: tokens,
                    EstimatedCostUsd: cost));
            }
            catch (JsonException)
            {
                // A partially written JSONL tail is normal while an agent is active.
            }
            finally
            {
                document?.Dispose();
            }
        }

        if (turns.Count == 0)
        {
            return null;
        }

        turns = turns.OrderBy(x => x.At).TakeLast(250).ToList();
        sessionId ??= Path.GetFileNameWithoutExtension(filePath);
        cwd ??= InferProjectPath(filePath, provider);
        model ??= provider switch
        {
            AgentProvider.Claude => "claude (unreported)",
            AgentProvider.Codex => "codex (unreported)",
            _ => "copilot (unreported)"
        };
        agent ??= "primary";
        first ??= turns[0].At;
        last ??= turns[^1].At;

        var total = Sum(turns.Select(x => x.Tokens));
        var state = (DateTimeOffset.UtcNow - last.Value) switch
        {
            var age when age <= TimeSpan.FromMinutes(5) => SessionState.Active,
            var age when age <= TimeSpan.FromMinutes(30) => SessionState.Waiting,
            var age when age <= TimeSpan.FromHours(8) => SessionState.Idle,
            _ => SessionState.Completed
        };
        var projectAlias = ProjectAlias(cwd);

        return new AgentSession(
            Id: sessionId,
            Provider: provider,
            Project: redactor.Text(cwd, 160),
            ProjectAlias: projectAlias,
            Agent: redactor.Text(agent, 48),
            Model: redactor.Text(model, 64),
            State: state,
            StartedAt: first.Value,
            LastActivityAt: last.Value,
            Tokens: total,
            EstimatedCostUsd: turns.Sum(x => x.EstimatedCostUsd),
            Turns: turns);
    }

    public static TokenUsage Sum(IEnumerable<TokenUsage> values) => values.Aggregate(
        new TokenUsage(),
        (a, b) => new TokenUsage(a.Input + b.Input, a.Output + b.Output, a.CacheRead + b.CacheRead, a.CacheWrite + b.CacheWrite));

    private static TokenUsage ExtractTokens(JsonElement root)
    {
        return new(
            Input: FirstLong(root, "input_tokens", "inputTokens", "prompt_tokens", "promptTokens"),
            Output: FirstLong(root, "output_tokens", "outputTokens", "completion_tokens", "completionTokens"),
            CacheRead: FirstLong(root, "cache_read_input_tokens", "cacheReadInputTokens", "cached_tokens", "cachedTokens"),
            CacheWrite: FirstLong(root, "cache_creation_input_tokens", "cacheCreationInputTokens", "cache_write_tokens", "cacheWriteTokens"));
    }

    private static TurnKind InferKind(JsonElement root)
    {
        var role = FirstString(root, "role", "type", "event", "kind")?.ToLowerInvariant() ?? string.Empty;
        if (role.Contains("tool") || FirstString(root, "tool_name", "toolName") is not null) return TurnKind.Tool;
        if (role.Contains("user") || role.Contains("prompt")) return TurnKind.User;
        if (role.Contains("assistant") || role.Contains("message")) return TurnKind.Assistant;
        if (role.Contains("result") || role.Contains("output")) return TurnKind.Result;
        return TurnKind.System;
    }

    private static string ExtractSummary(JsonElement root)
    {
        foreach (var key in new[] { "summary", "prompt", "text", "message", "content", "output", "result" })
        {
            if (TryFind(root, key, out var found))
            {
                var text = ElementText(found);
                if (!string.IsNullOrWhiteSpace(text)) return text;
            }
        }
        return string.Empty;
    }

    private static string ElementText(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.String => element.GetString() ?? string.Empty,
        JsonValueKind.Array => string.Join(" ", element.EnumerateArray().Select(ElementText).Where(x => x.Length > 0).Take(4)),
        JsonValueKind.Object when element.TryGetProperty("text", out var text) => ElementText(text),
        JsonValueKind.Object when element.TryGetProperty("content", out var content) => ElementText(content),
        _ => string.Empty
    };

    private static string? FirstString(JsonElement root, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (TryFind(root, key, out var value) && value.ValueKind == JsonValueKind.String)
            {
                var result = value.GetString();
                if (!string.IsNullOrWhiteSpace(result)) return result;
            }
        }
        return null;
    }

    private static long FirstLong(JsonElement root, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (!TryFind(root, key, out var value)) continue;
            if (value.TryGetInt64(out var number)) return number;
            if (value.ValueKind == JsonValueKind.String && long.TryParse(value.GetString(), out number)) return number;
        }
        return 0;
    }

    private static DateTimeOffset? FirstDate(JsonElement root, params string[] keys)
    {
        var value = FirstString(root, keys);
        if (value is not null && DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var parsed))
        {
            return parsed;
        }
        foreach (var key in keys)
        {
            if (TryFind(root, key, out var element) && element.TryGetInt64(out var epoch))
            {
                try { return epoch > 10_000_000_000 ? DateTimeOffset.FromUnixTimeMilliseconds(epoch) : DateTimeOffset.FromUnixTimeSeconds(epoch); }
                catch (ArgumentOutOfRangeException) { }
            }
        }
        return null;
    }

    private static bool TryFind(JsonElement root, string key, out JsonElement value, int depth = 0)
    {
        if (depth > 6) { value = default; return false; }
        if (root.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in root.EnumerateObject())
            {
                if (property.Name.Equals(key, StringComparison.OrdinalIgnoreCase)) { value = property.Value; return true; }
            }
            foreach (var property in root.EnumerateObject())
            {
                if (TryFind(property.Value, key, out value, depth + 1)) return true;
            }
        }
        else if (root.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in root.EnumerateArray().Take(12))
            {
                if (TryFind(item, key, out value, depth + 1)) return true;
            }
        }
        value = default;
        return false;
    }

    private static string ProjectAlias(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return "unknown-project";
        var normalized = path.TrimEnd('/', '\\').Replace('\\', '/');
        var last = normalized.Split('/', StringSplitOptions.RemoveEmptyEntries).LastOrDefault() ?? "project";
        return last.Length <= 30 ? last : last[..29] + "…";
    }

    private static string InferProjectPath(string path, AgentProvider provider)
    {
        var directory = Path.GetDirectoryName(path) ?? path;
        if (provider == AgentProvider.Copilot) return Path.GetFileName(Path.GetDirectoryName(directory)) ?? directory;
        return directory;
    }

    private static string StableId(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)))[..12].ToLowerInvariant();
}
