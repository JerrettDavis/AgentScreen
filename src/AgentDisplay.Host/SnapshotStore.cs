using AgentDisplay.Contracts;
using AgentDisplay.Core;

namespace AgentDisplay.Host;

public sealed class SnapshotStore(MetricsCalculator metrics, DeviceSnapshotMapper deviceMapper)
{
    private readonly object _gate = new();
    private IReadOnlyList<AgentSession> _sessions = [];
    private IReadOnlyList<ProviderUsage> _usage = [];
    private IReadOnlyList<DirectoryStatus> _directories = [];
    private readonly Dictionary<string, GateRequest> _gates = new(StringComparer.OrdinalIgnoreCase);
    private bool _demoMode;

    public void Replace(IReadOnlyList<AgentSession> sessions, IReadOnlyList<ProviderUsage> usage, IReadOnlyList<DirectoryStatus> directories, bool demoMode)
    {
        lock (_gate)
        {
            _sessions = sessions;
            _usage = usage;
            _directories = directories;
            _demoMode = demoMode;
        }
    }

    public DashboardSnapshot Snapshot()
    {
        lock (_gate)
        {
            var now = DateTimeOffset.UtcNow;
            var gates = _gates.Values.OrderByDescending(x => x.CreatedAt).ToArray();
            return new DashboardSnapshot(
                Protocol.ProductVersion,
                now,
                _usage,
                _sessions.OrderByDescending(x => x.LastActivityAt).ToArray(),
                metrics.Calculate(_sessions, _usage, now),
                gates,
                _directories,
                _demoMode,
                Environment.MachineName);
        }
    }

    public DeviceSnapshot DeviceSnapshot() => deviceMapper.Map(Snapshot());

    public AgentSession? Session(string id)
    {
        lock (_gate) return _sessions.FirstOrDefault(x => x.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
    }

    public GateRequest AddGate(GateRequest gate)
    {
        lock (_gate) { _gates[gate.Id] = gate; return gate; }
    }

    public GateRequest? Gate(string id)
    {
        lock (_gate) return _gates.GetValueOrDefault(id);
    }

    public GateRequest? Decide(string id, PolicyDecision decision, string actor)
    {
        lock (_gate)
        {
            if (!_gates.TryGetValue(id, out var existing) || existing.State != GateState.Pending) return existing;
            var state = decision == PolicyDecision.Allow ? GateState.Allowed : GateState.Denied;
            var updated = existing with { State = state, DecidedAt = DateTimeOffset.UtcNow, DecidedBy = actor };
            _gates[id] = updated;
            return updated;
        }
    }

    public void ExpireGates(DateTimeOffset now)
    {
        lock (_gate)
        {
            foreach (var item in _gates.Where(x => x.Value.State == GateState.Pending && x.Value.ExpiresAt <= now).ToArray())
                _gates[item.Key] = item.Value with { State = GateState.Expired, DecidedAt = now, DecidedBy = "timeout" };
            foreach (var key in _gates.Where(x => x.Value.CreatedAt < now.AddHours(-24)).Select(x => x.Key).ToArray()) _gates.Remove(key);
        }
    }
}
