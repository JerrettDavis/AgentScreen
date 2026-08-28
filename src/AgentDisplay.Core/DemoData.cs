using AgentDisplay.Contracts;

namespace AgentDisplay.Core;

public static class DemoData
{
    public static (IReadOnlyList<AgentSession> Sessions, IReadOnlyList<ProviderUsage> Usage, IReadOnlyList<GateRequest> Gates) Create(DateTimeOffset now)
    {
        var claudeTurns = new[]
        {
            Turn("c1", now.AddMinutes(-34), TurnKind.User, "Implement the device pairing flow and keep credentials on the host.", 2_480, 0, 48_100, 1_200, 0.0187m),
            Turn("c2", now.AddMinutes(-31), TurnKind.Assistant, "Mapped the host, PWA, and display trust boundaries.", 1_140, 1_820, 47_900, 0, 0.0261m),
            Turn("c3", now.AddMinutes(-18), TurnKind.Tool, "dotnet test AgentDisplay.slnx", 620, 210, 48_500, 0, 0.0089m, "Bash"),
            Turn("c4", now.AddMinutes(-2), TurnKind.Assistant, "Added the compact display snapshot and approval queue.", 910, 2_140, 49_020, 0, 0.0304m)
        };
        var codexTurns = new[]
        {
            Turn("x1", now.AddMinutes(-51), TurnKind.User, "Review the parser fixtures for schema drift.", 3_100, 0, 20_400, 0, 0.0102m),
            Turn("x2", now.AddMinutes(-46), TurnKind.Tool, "rg token_usage tests/fixtures", 380, 84, 21_010, 0, 0.0021m, "shell"),
            Turn("x3", now.AddMinutes(-7), TurnKind.Assistant, "Normalized nested token fields without binding to one transcript version.", 1_250, 2_980, 0, 0, 0.0448m)
        };
        var copilotTurns = new[]
        {
            Turn("g1", now.AddMinutes(-14), TurnKind.User, "Style the mobile dashboard and approval sheet.", 1_880, 0, 12_400, 0, 0.0079m),
            Turn("g2", now.AddMinutes(-10), TurnKind.Assistant, "Refined responsive cards, status chips, and bottom navigation.", 720, 1_630, 12_960, 0, 0.0182m)
        };
        var sessions = new[]
        {
            Session("claude-agentdisplay", AgentProvider.Claude, "AgentDisplay", "primary", "claude-sonnet-5", SessionState.Active, now.AddHours(-1.2), claudeTurns),
            Session("codex-parser", AgentProvider.Codex, "AgentDisplay", "reviewer", "gpt-5.3-codex", SessionState.Active, now.AddHours(-1.0), codexTurns),
            Session("copilot-pwa", AgentProvider.Copilot, "AgentDisplay.Web", "frontend", "gpt-5", SessionState.Waiting, now.AddMinutes(-39), copilotTurns),
            Session("claude-firmware", AgentProvider.Claude, "E32R40T", "firmware", "claude-sonnet-4.6", SessionState.Idle, now.AddHours(-4), new[] { Turn("f1", now.AddHours(-3.2), TurnKind.Result, "Validated the ST7796S display pin map and LVGL buffer sizing.", 8_400, 2_210, 62_000, 4_200, 0.082m) })
        };
        var usage = new[]
        {
            new ProviderUsage(AgentProvider.Claude, true, "OAuth usage connected", new[]
            {
                new UsageWindow("5h", "5 hour", 68, now.AddHours(2.1), 1.42m, MetricSource.Provider),
                new UsageWindow("7d", "7 day", 43, now.AddDays(4.4), null, MetricSource.Provider)
            }, Sum(sessions.Where(x => x.Provider == AgentProvider.Claude)), sessions.Where(x => x.Provider == AgentProvider.Claude).Sum(x => x.EstimatedCostUsd), MetricSource.Provider, now.AddSeconds(-22)),
            new ProviderUsage(AgentProvider.Codex, true, "Local transcript estimate", new[]
            {
                new UsageWindow("5h-est", "5 hour est.", 31, now.AddHours(3.4), null, MetricSource.Estimated, "Observed local activity")
            }, Sum(sessions.Where(x => x.Provider == AgentProvider.Codex)), sessions.Where(x => x.Provider == AgentProvider.Codex).Sum(x => x.EstimatedCostUsd), MetricSource.LocalTranscript, now.AddSeconds(-8)),
            new ProviderUsage(AgentProvider.Copilot, true, "Local session-state", new[]
            {
                new UsageWindow("observed", "observed", 19, null, null, MetricSource.LocalTranscript, "Not an entitlement window")
            }, Sum(sessions.Where(x => x.Provider == AgentProvider.Copilot)), sessions.Where(x => x.Provider == AgentProvider.Copilot).Sum(x => x.EstimatedCostUsd), MetricSource.LocalTranscript, now.AddSeconds(-14))
        };
        var gates = new[]
        {
            new GateRequest("gate-demo-publish", "claude-agentdisplay", AgentProvider.Claude, "AgentDisplay", "Bash", "npm publish --access public", "Publishing or deployment requires approval", GateState.Pending, now.AddSeconds(-18), now.AddMinutes(1.7))
        };
        return (sessions, usage, gates);
    }

    private static AgentTurn Turn(string id, DateTimeOffset at, TurnKind kind, string summary, long input, long output, long read, long write, decimal cost, string? tool = null) =>
        new(id, at, kind, summary, tool, null, new TokenUsage(input, output, read, write), cost);

    private static AgentSession Session(string id, AgentProvider provider, string project, string agent, string model, SessionState state, DateTimeOffset started, IReadOnlyList<AgentTurn> turns)
    {
        var tokens = AgentLogParser.Sum(turns.Select(x => x.Tokens));
        return new(id, provider, $"~/src/{project}", project, agent, model, state, started, turns.Max(x => x.At), tokens, turns.Sum(x => x.EstimatedCostUsd), turns, MetricSource.Demo);
    }

    private static TokenUsage Sum(IEnumerable<AgentSession> sessions) => AgentLogParser.Sum(sessions.Select(x => x.Tokens));
}
