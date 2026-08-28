using AgentDisplay.Contracts;
using AgentDisplay.Core;
using Microsoft.Extensions.Options;

namespace AgentDisplay.Host;

public sealed class AgentScannerService(
    SnapshotStore store,
    DirectoryLocator directories,
    AgentLogParser parser,
    ClaudeUsageCollector claudeUsage,
    CodexUsageCollector codexUsage,
    IOptionsMonitor<AgentDisplayOptions> options,
    ILogger<AgentScannerService> logger) : BackgroundService
{
    public bool ForceDemo { get; set; }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try { await ScanAsync(stoppingToken); }
            catch (Exception ex) { logger.LogError(ex, "Agent directory scan failed"); }
            await Task.Delay(TimeSpan.FromSeconds(Math.Max(1, options.CurrentValue.ScanIntervalSeconds)), stoppingToken);
        }
    }

    public async Task ScanAsync(CancellationToken cancellationToken)
    {
        var sessions = new List<AgentSession>();
        var statuses = new List<DirectoryStatus>();
        foreach (var provider in Enum.GetValues<AgentProvider>())
        {
            var root = directories.Root(provider);
            var files = Enumerate(provider, root).OrderByDescending(SafeWriteTime).Take(options.CurrentValue.MaxFilesPerProvider).ToArray();
            foreach (var file in files)
            {
                try
                {
                    var lines = await TailReader.ReadLinesAsync(file, options.CurrentValue.MaxBytesPerFile, cancellationToken);
                    var session = parser.Parse(provider, file, lines);
                    if (session is not null) sessions.Add(session);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    logger.LogDebug(ex, "Skipped agent file {Path}", file);
                }
            }
            statuses.Add(new DirectoryStatus(provider, DirectoryLocator.Display(root), Directory.Exists(root), files.Length, DateTimeOffset.UtcNow,
                files.Length == 0 ? "No readable session files" : $"{files.Length} recent file{(files.Length == 1 ? string.Empty : "s")}"));
        }

        sessions = sessions.GroupBy(x => $"{x.Provider}:{x.Id}", StringComparer.OrdinalIgnoreCase)
            .Select(x => x.OrderByDescending(s => s.LastActivityAt).First())
            .OrderByDescending(x => x.LastActivityAt).ToList();

        if (ForceDemo || sessions.Count == 0)
        {
            var demo = DemoData.Create(DateTimeOffset.UtcNow);
            store.Replace(demo.Sessions, demo.Usage, statuses, demoMode: true);
            foreach (var gate in demo.Gates) if (store.Gate(gate.Id) is null) store.AddGate(gate);
            return;
        }

        var usage = await BuildUsageAsync(sessions, cancellationToken);
        store.Replace(sessions, usage, statuses, demoMode: false);
    }

    private async Task<IReadOnlyList<ProviderUsage>> BuildUsageAsync(IReadOnlyList<AgentSession> sessions, CancellationToken cancellationToken)
    {
        var result = new List<ProviderUsage>();
        foreach (var provider in Enum.GetValues<AgentProvider>())
        {
            var providerSessions = sessions.Where(x => x.Provider == provider).ToArray();
            var tokens = AgentLogParser.Sum(providerSessions.Select(x => x.Tokens));
            var cost = providerSessions.Sum(x => x.EstimatedCostUsd);
            if (provider == AgentProvider.Claude)
            {
                var authoritative = await claudeUsage.TryCollectAsync(tokens, cost, cancellationToken);
                if (authoritative is not null) { result.Add(authoritative); continue; }
            }
            if (provider == AgentProvider.Codex)
            {
                var authoritative = await codexUsage.TryCollectAsync(tokens, cost, cancellationToken);
                if (authoritative is not null) { result.Add(authoritative); continue; }
            }

            var activeTokens = providerSessions.Where(x => x.LastActivityAt > DateTimeOffset.UtcNow.AddHours(-5)).Sum(x => x.Tokens.Total);
            var observedPercent = Math.Clamp(activeTokens / 2_500_000d * 100d, 0, 99);
            result.Add(new ProviderUsage(
                provider,
                providerSessions.Length > 0,
                providerSessions.Length == 0 ? "No local sessions" : "Local transcript estimate",
                providerSessions.Length == 0 ? [] : [new UsageWindow("observed", "observed", observedPercent, null, null, MetricSource.LocalTranscript, "Not an entitlement window")],
                tokens,
                cost,
                MetricSource.LocalTranscript,
                DateTimeOffset.UtcNow));
        }
        return result;
    }

    private static IEnumerable<string> Enumerate(AgentProvider provider, string root)
    {
        if (!Directory.Exists(root)) return [];
        try
        {
            // Directory.EnumerateFiles is lazy, so materialize inside this try
            // block. Otherwise access failures can escape while the caller is
            // sorting the returned sequence.
            return (provider switch
            {
                AgentProvider.Claude => EnumerateUnder(Path.Combine(root, "projects"), "*.jsonl"),
                AgentProvider.Codex => EnumerateUnder(Path.Combine(root, "sessions"), "*.jsonl")
                    .Concat(File.Exists(Path.Combine(root, "history.jsonl")) ? [Path.Combine(root, "history.jsonl")] : []),
                AgentProvider.Copilot => EnumerateUnder(Path.Combine(root, "session-state"), "events.jsonl"),
                _ => []
            }).ToArray();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return []; }
    }

    private static IEnumerable<string> EnumerateUnder(string root, string pattern) => Directory.Exists(root)
        ? Directory.EnumerateFiles(root, pattern, SearchOption.AllDirectories)
        : [];

    private static DateTime SafeWriteTime(string path)
    {
        try { return File.GetLastWriteTimeUtc(path); }
        catch { return DateTime.MinValue; }
    }
}
