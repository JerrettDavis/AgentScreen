using AgentDisplay.Contracts;

namespace AgentDisplay.Core;

public sealed record ModelPricing(
    string Key,
    decimal InputPerMillion,
    decimal OutputPerMillion,
    decimal CacheReadPerMillion,
    decimal CacheWritePerMillion);

public sealed class PricingCatalog
{
    public const string CatalogAsOf = "2026-08-27";

    private readonly IReadOnlyList<ModelPricing> _models =
    [
        new("claude-opus-5", 5m, 25m, 0.50m, 6.25m),
        new("claude-sonnet-5", 2m, 10m, 0.20m, 2.50m),
        new("claude-sonnet-4-6", 3m, 15m, 0.30m, 3.75m),
        new("claude-sonnet-4.6", 3m, 15m, 0.30m, 3.75m),
        new("gpt-5.3-codex", 1.75m, 14m, 0.175m, 1.75m),
        new("gpt-5-codex", 1.25m, 10m, 0.125m, 1.25m),
        new("gpt-5", 1.25m, 10m, 0.125m, 1.25m),
        new("claude", 3m, 15m, 0.30m, 3.75m),
        new("codex", 1.75m, 14m, 0.175m, 1.75m),
        new("copilot", 1.75m, 14m, 0.175m, 1.75m)
    ];

    public ModelPricing Find(string? model, AgentProvider provider)
    {
        var candidate = model?.Trim().ToLowerInvariant() ?? string.Empty;
        var exact = _models.FirstOrDefault(x => candidate.Contains(x.Key, StringComparison.OrdinalIgnoreCase));
        if (exact is not null)
        {
            return exact;
        }

        var fallback = provider switch
        {
            AgentProvider.Claude => "claude",
            AgentProvider.Codex => "codex",
            _ => "copilot"
        };
        return _models.First(x => x.Key == fallback);
    }

    public decimal Estimate(TokenUsage usage, string? model, AgentProvider provider)
    {
        var price = Find(model, provider);
        const decimal million = 1_000_000m;
        return decimal.Round(
            usage.Input / million * price.InputPerMillion +
            usage.Output / million * price.OutputPerMillion +
            usage.CacheRead / million * price.CacheReadPerMillion +
            usage.CacheWrite / million * price.CacheWritePerMillion,
            6,
            MidpointRounding.AwayFromZero);
    }
}
