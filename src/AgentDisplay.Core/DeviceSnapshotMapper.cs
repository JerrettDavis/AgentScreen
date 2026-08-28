using AgentDisplay.Contracts;

namespace AgentDisplay.Core;

public sealed class DeviceSnapshotMapper
{
    public DeviceSnapshot Map(DashboardSnapshot snapshot)
    {
        var providers = snapshot.Providers.Select(x => new DeviceProvider(
            N: x.Provider.ToString(),
            U: (int)Math.Round(x.Windows.FirstOrDefault()?.UsedPercent ?? 0),
            W: x.Windows.FirstOrDefault()?.Label ?? "observed",
            C: x.Connected)).ToArray();

        var sessions = snapshot.Sessions
            .OrderByDescending(x => x.LastActivityAt)
            .Take(8)
            .Select(x => new DeviceSession(
                I: x.Id.Length <= 12 ? x.Id : x.Id[..12],
                P: x.Provider.ToString(),
                A: x.ProjectAlias,
                M: x.Model.Length <= 22 ? x.Model : x.Model[..21] + "…",
                C: (int)Math.Min(int.MaxValue, x.EstimatedCostUsd * 10_000m),
                T: (int)Math.Min(int.MaxValue, x.Tokens.Total / 1_000),
                S: x.State.ToString())).ToArray();

        var pending = snapshot.Gates.FirstOrDefault(x => x.State == GateState.Pending);
        var gate = pending is null ? null : new DeviceGate(
            I: pending.Id,
            P: pending.ProjectAlias,
            T: pending.ToolName,
            Q: pending.Summary,
            R: pending.Reason,
            X: pending.ExpiresAt.ToUnixTimeSeconds());

        var stats = snapshot.Stats;
        return new DeviceSnapshot(
            V: Protocol.Version,
            Ts: snapshot.GeneratedAt.ToUnixTimeSeconds(),
            P: providers,
            S: sessions,
            M: new DeviceStats(
                A: stats.ActiveSessions,
                S: stats.SessionCount,
                P: stats.ProjectCount,
                G: snapshot.Gates.Count(x => x.State == GateState.Pending),
                D: stats.EstimatedSpendUsd,
                H: stats.CostPerHourUsd,
                R: (int)Math.Round(stats.CacheHitPercent),
                B: stats.CacheBreaks,
                X: stats.EstimatedExhaustionAt?.ToUnixTimeSeconds() ?? 0),
            G: gate);
    }
}
