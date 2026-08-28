using System.Text.Json.Serialization;

namespace AgentDisplay.Contracts;

[JsonConverter(typeof(JsonStringEnumConverter<AgentProvider>))]
public enum AgentProvider { Claude, Codex, Copilot }

[JsonConverter(typeof(JsonStringEnumConverter<SessionState>))]
public enum SessionState { Active, Waiting, Idle, Completed, Error }

[JsonConverter(typeof(JsonStringEnumConverter<TurnKind>))]
public enum TurnKind { User, Assistant, Tool, System, Result }

[JsonConverter(typeof(JsonStringEnumConverter<GateState>))]
public enum GateState { Pending, Allowed, Denied, Expired }

[JsonConverter(typeof(JsonStringEnumConverter<PolicyDecision>))]
public enum PolicyDecision { Allow, Ask, Deny }

[JsonConverter(typeof(JsonStringEnumConverter<MetricSource>))]
public enum MetricSource { Provider, LocalTranscript, Estimated, Demo }

public sealed record TokenUsage(
    long Input = 0,
    long Output = 0,
    long CacheRead = 0,
    long CacheWrite = 0)
{
    public long Total => Input + Output + CacheRead + CacheWrite;
}

public sealed record AgentTurn(
    string Id,
    DateTimeOffset At,
    TurnKind Kind,
    string Summary,
    string? ToolName,
    PolicyDecision? Decision,
    TokenUsage Tokens,
    decimal EstimatedCostUsd = 0m);

public sealed record AgentSession(
    string Id,
    AgentProvider Provider,
    string Project,
    string ProjectAlias,
    string Agent,
    string Model,
    SessionState State,
    DateTimeOffset StartedAt,
    DateTimeOffset LastActivityAt,
    TokenUsage Tokens,
    decimal EstimatedCostUsd,
    IReadOnlyList<AgentTurn> Turns,
    MetricSource Source = MetricSource.LocalTranscript)
{
    public TimeSpan Age => DateTimeOffset.UtcNow - StartedAt;
}

public sealed record UsageWindow(
    string Key,
    string Label,
    double UsedPercent,
    DateTimeOffset? ResetsAt,
    decimal? OverageUsd,
    MetricSource Source,
    string? Note = null);

public sealed record ProviderUsage(
    AgentProvider Provider,
    bool Connected,
    string Status,
    IReadOnlyList<UsageWindow> Windows,
    TokenUsage ObservedTokens,
    decimal EstimatedCostUsd,
    MetricSource Source,
    DateTimeOffset? RefreshedAt = null);

public sealed record DashboardStats(
    int ActiveSessions,
    int SessionCount,
    int AgentCount,
    int ProjectCount,
    decimal EstimatedSpendUsd,
    decimal CostPerMinuteUsd,
    decimal CostPerHourUsd,
    long CachedTokens,
    int CacheBreaks,
    double CacheHitPercent,
    DateTimeOffset? EstimatedExhaustionAt,
    decimal EstimatedSpendAtExhaustionUsd,
    string ForecastNote);

public sealed record GateRequest(
    string Id,
    string SessionId,
    AgentProvider Provider,
    string ProjectAlias,
    string ToolName,
    string Summary,
    string Reason,
    GateState State,
    DateTimeOffset CreatedAt,
    DateTimeOffset ExpiresAt,
    DateTimeOffset? DecidedAt = null,
    string? DecidedBy = null);

public sealed record DirectoryStatus(
    AgentProvider Provider,
    string DisplayPath,
    bool Exists,
    int Files,
    DateTimeOffset? LastScanAt,
    string Coverage);

public sealed record DashboardSnapshot(
    string Version,
    DateTimeOffset GeneratedAt,
    IReadOnlyList<ProviderUsage> Providers,
    IReadOnlyList<AgentSession> Sessions,
    DashboardStats Stats,
    IReadOnlyList<GateRequest> Gates,
    IReadOnlyList<DirectoryStatus> Directories,
    bool DemoMode,
    string HostName);

public sealed record HookEvent(
    AgentProvider Provider,
    string EventName,
    string? SessionId,
    string? Cwd,
    string? ToolName,
    object? ToolInput,
    string? Prompt,
    string? Model,
    DateTimeOffset ReceivedAt,
    IReadOnlyDictionary<string, object?>? Metadata = null);

public sealed record HookResponse(
    string EventId,
    PolicyDecision Decision,
    string Reason,
    string? GateId = null,
    int PollAfterMs = 350,
    DateTimeOffset? ExpiresAt = null);

public sealed record GateDecisionRequest(PolicyDecision Decision, string Actor = "pwa");

public sealed record HookInstallRequest(AgentProvider? Provider, bool DryRun = true);
public sealed record HookInstallResult(AgentProvider Provider, string Path, bool Changed, bool DryRun, string Message, string? BackupPath = null);

public sealed record DevicePairing(
    string HostUrl,
    IReadOnlyList<string> HostCandidates,
    string PairingKey,
    string BluetoothServiceUuid,
    string BluetoothRxUuid,
    string BluetoothTxUuid);

public sealed record DeviceSnapshot(
    string V,
    long Ts,
    IReadOnlyList<DeviceProvider> P,
    IReadOnlyList<DeviceSession> S,
    DeviceStats M,
    DeviceGate? G);

public sealed record DeviceProvider(string N, int U, string W, bool C);
public sealed record DeviceSession(string I, string P, string A, string M, int C, int T, string S);
public sealed record DeviceStats(int A, int S, int P, int G, decimal D, decimal H, int R, int B, long X);
public sealed record DeviceGate(string I, string P, string T, string Q, string R, long X);

public static class Protocol
{
    public const string Version = "1";
    public const string ProductVersion = "0.1.0-alpha.1";
    public const string BluetoothServiceUuid = "9f5e0001-4a67-4f3b-a7d0-a1d4a7d10001";
    public const string BluetoothRxUuid = "9f5e0002-4a67-4f3b-a7d0-a1d4a7d10001";
    public const string BluetoothTxUuid = "9f5e0003-4a67-4f3b-a7d0-a1d4a7d10001";
}
