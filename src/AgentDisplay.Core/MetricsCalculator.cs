using AgentDisplay.Contracts;

namespace AgentDisplay.Core;

public sealed class MetricsCalculator
{
    public DashboardStats Calculate(IReadOnlyList<AgentSession> sessions, IReadOnlyList<ProviderUsage> providers, DateTimeOffset now)
    {
        var active = sessions.Count(x => x.State is SessionState.Active or SessionState.Waiting);
        var spend = sessions.Sum(x => x.EstimatedCostUsd);
        var recentCost = sessions.SelectMany(x => x.Turns).Where(x => x.At >= now.AddMinutes(-60)).Sum(x => x.EstimatedCostUsd);
        var activeMinutes = Math.Clamp(
            sessions.Where(x => x.LastActivityAt >= now.AddMinutes(-60)).Sum(x => Math.Min(60, Math.Max(1, (now - x.StartedAt).TotalMinutes))),
            1,
            60 * Math.Max(1, active));
        var perMinute = decimal.Round(recentCost / (decimal)activeMinutes, 5);
        var perHour = decimal.Round(perMinute * 60m, 4);
        var allTurns = sessions.SelectMany(x => x.Turns).OrderBy(x => x.At).ToArray();
        var cached = sessions.Sum(x => x.Tokens.CacheRead);
        var cacheEligible = sessions.Sum(x => x.Tokens.Input + x.Tokens.CacheRead);
        var cacheHit = cacheEligible == 0 ? 0 : cached * 100d / cacheEligible;
        var cacheBreaks = CountCacheBreaks(allTurns);
        var exhaustion = ForecastExhaustion(providers, now);
        var futureMinutes = exhaustion is null ? 0m : (decimal)Math.Max(0, (exhaustion.Value - now).TotalMinutes);

        return new DashboardStats(
            ActiveSessions: active,
            SessionCount: sessions.Count,
            AgentCount: sessions.Select(x => $"{x.Provider}:{x.Agent}").Distinct(StringComparer.OrdinalIgnoreCase).Count(),
            ProjectCount: sessions.Select(x => x.ProjectAlias).Distinct(StringComparer.OrdinalIgnoreCase).Count(),
            EstimatedSpendUsd: decimal.Round(spend, 4),
            CostPerMinuteUsd: perMinute,
            CostPerHourUsd: perHour,
            CachedTokens: cached,
            CacheBreaks: cacheBreaks,
            CacheHitPercent: Math.Round(cacheHit, 1),
            EstimatedExhaustionAt: exhaustion,
            EstimatedSpendAtExhaustionUsd: decimal.Round(spend + perMinute * futureMinutes, 2),
            ForecastNote: exhaustion is null ? "Not enough authoritative window data" : "Linear projection from the most constrained reported window");
    }

    private static int CountCacheBreaks(IReadOnlyList<AgentTurn> turns)
    {
        var breaks = 0;
        var hadCache = false;
        foreach (var turn in turns)
        {
            if (turn.Tokens.CacheRead > 0) hadCache = true;
            else if (hadCache && turn.Tokens.Input > 1_000) { breaks++; hadCache = false; }
        }
        return breaks;
    }

    private static DateTimeOffset? ForecastExhaustion(IReadOnlyList<ProviderUsage> providers, DateTimeOffset now)
    {
        var candidate = providers.SelectMany(x => x.Windows)
            .Where(x => x.UsedPercent is > 1 and < 100 && x.ResetsAt > now)
            .Select(x =>
            {
                var durationHours = x.Key.Contains("7", StringComparison.OrdinalIgnoreCase) ? 168d : 5d;
                var start = x.ResetsAt!.Value.AddHours(-durationHours);
                var elapsedHours = Math.Max(0.05, (now - start).TotalHours);
                var rate = x.UsedPercent / elapsedHours;
                var remainingHours = rate <= 0 ? double.MaxValue : (100d - x.UsedPercent) / rate;
                return now.AddHours(Math.Min(remainingHours, (x.ResetsAt.Value - now).TotalHours));
            })
            .OrderBy(x => x)
            .FirstOrDefault();
        return candidate == default ? null : candidate;
    }
}
