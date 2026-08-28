using System.Text.Json;
using AgentDisplay.Contracts;
using AgentDisplay.Core;
using Microsoft.Extensions.Options;

namespace AgentDisplay.Host;

public sealed class HookProcessor(PolicyEngine policy, Redactor redactor, SnapshotStore store, IOptionsMonitor<AgentDisplayOptions> options)
{
    public HookResponse Process(HookEvent hookEvent)
    {
        var eventId = Guid.NewGuid().ToString("n");
        if (!IsGateEvent(hookEvent.EventName))
            return new(eventId, PolicyDecision.Allow, "Lifecycle event recorded");

        var result = policy.Evaluate(hookEvent);
        if (result.Decision != PolicyDecision.Ask)
            return new(eventId, result.Decision, result.Reason);

        var now = DateTimeOffset.UtcNow;
        var gateId = "gate-" + Guid.NewGuid().ToString("n")[..16];
        var project = Alias(hookEvent.Cwd);
        var summary = Summarize(hookEvent.ToolInput, hookEvent.Prompt);
        var gate = new GateRequest(
            gateId,
            hookEvent.SessionId ?? "unknown",
            hookEvent.Provider,
            project,
            redactor.Text(hookEvent.ToolName ?? "tool", 48),
            summary,
            result.Reason,
            GateState.Pending,
            now,
            now.AddSeconds(Math.Max(10, options.CurrentValue.GateTimeoutSeconds)));
        store.AddGate(gate);
        return new(eventId, PolicyDecision.Ask, result.Reason, gateId, 350, gate.ExpiresAt);
    }

    public PolicyDecision ResolveExpired() => options.CurrentValue.StrictGates ? PolicyDecision.Deny : PolicyDecision.Allow;

    private string Summarize(object? input, string? prompt)
    {
        var text = input switch
        {
            JsonElement json when json.ValueKind != JsonValueKind.Undefined => json.GetRawText(),
            null => prompt,
            _ => JsonSerializer.Serialize(input)
        };
        return redactor.Text(text, 160);
    }

    private static bool IsGateEvent(string name) => name.Equals("PreToolUse", StringComparison.OrdinalIgnoreCase)
        || name.Equals("preToolUse", StringComparison.OrdinalIgnoreCase)
        || name.Equals("PermissionRequest", StringComparison.OrdinalIgnoreCase)
        || name.Equals("permissionRequest", StringComparison.OrdinalIgnoreCase);

    private static string Alias(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return "unknown-project";
        return Path.GetFileName(path.TrimEnd('/', '\\')) is { Length: > 0 } value ? value : "project";
    }
}
