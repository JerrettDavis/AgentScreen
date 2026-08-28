using System.Text.Json;
using System.Text.RegularExpressions;
using AgentDisplay.Contracts;

namespace AgentDisplay.Core;

public sealed record PolicyRule(string Id, PolicyDecision Decision, Regex Pattern, string Reason);
public sealed record PolicyResult(PolicyDecision Decision, string Reason, string RuleId);

public sealed class PolicyEngine
{
    private readonly IReadOnlyList<PolicyRule> _rules =
    [
        Rule("deny-credential-exfiltration", PolicyDecision.Deny,
            @"(?ix)(curl|wget|invoke-webrequest).*(\.ssh|\.aws|\.azure|auth\.json|credentials|\.credentials\.json)",
            "Potential credential exfiltration"),
        Rule("ask-destructive-shell", PolicyDecision.Ask,
            @"(?ix)(rm\s+-rf|git\s+reset\s+--hard|git\s+clean\s+-[a-z]*f|remove-item\s+.*-recurse|format\s+[a-z]:|diskpart|drop\s+(database|table))",
            "Destructive operation requires approval"),
        Rule("ask-publish-deploy", PolicyDecision.Ask,
            @"(?ix)(npm\s+publish|dotnet\s+nuget\s+push|nuget\s+push|docker\s+push|kubectl\s+(apply|delete)|terraform\s+apply|az\s+webapp\s+deploy)",
            "Publishing or deployment requires approval"),
        Rule("ask-sensitive-write", PolicyDecision.Ask,
            @"(?ix)(\.ssh|\.aws|\.azure|\.config[/\\]gcloud|auth\.json|credentials).*(write|edit|create|append|redirect|>)",
            "Writing a sensitive configuration path requires approval"),
        Rule("ask-system-service", PolicyDecision.Ask,
            @"(?ix)(systemctl\s+(stop|disable)|sc(\.exe)?\s+(stop|delete)|net\s+user|chmod\s+777)",
            "System-level change requires approval")
    ];

    public PolicyResult Evaluate(HookEvent hookEvent)
    {
        var input = Flatten(hookEvent);
        foreach (var rule in _rules)
        {
            if (rule.Pattern.IsMatch(input))
            {
                return new(rule.Decision, rule.Reason, rule.Id);
            }
        }

        return new(PolicyDecision.Allow, "No gate rule matched", "default-allow");
    }

    public IReadOnlyList<(string Id, PolicyDecision Decision, string Reason, string Pattern)> Describe() =>
        _rules.Select(x => (x.Id, x.Decision, x.Reason, x.Pattern.ToString())).ToArray();

    private static PolicyRule Rule(string id, PolicyDecision decision, string pattern, string reason) =>
        new(id, decision, new Regex(pattern, RegexOptions.Compiled | RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100)), reason);

    private static string Flatten(HookEvent hookEvent)
    {
        var input = hookEvent.ToolInput switch
        {
            null => string.Empty,
            JsonElement json => json.GetRawText(),
            _ => JsonSerializer.Serialize(hookEvent.ToolInput)
        };
        return string.Join(' ', hookEvent.EventName, hookEvent.ToolName, hookEvent.Prompt, hookEvent.Cwd, input);
    }
}
