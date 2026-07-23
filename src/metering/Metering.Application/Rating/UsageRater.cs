using Metering.Application.Abstractions;

namespace Metering.Application.Rating;

/// <summary>
/// Versioned unit-rate table (USD per 1M tokens). Unknown models fall back to the
/// Opus tier so cost is never silently under-counted. GCP billing-export reconciliation
/// for cloud meters is a later slice.
/// </summary>
public sealed class UsageRater : IUsageRater
{
    public string Version => "2026-01";

    // (input, output, cache_read, cache_write) USD per 1,000,000 tokens.
    private static readonly Dictionary<string, Rates> Table = new(StringComparer.OrdinalIgnoreCase)
    {
        ["claude-opus-4-8"] = new(5.00m, 25.00m, 0.50m, 6.25m),
        ["claude-sonnet-5"] = new(3.00m, 15.00m, 0.30m, 3.75m),
    };

    private static readonly Rates Fallback = new(5.00m, 25.00m, 0.50m, 6.25m);

    public decimal RateAiTokens(string model, string direction, long tokens)
    {
        if (tokens <= 0) return 0m;
        var r = Table.TryGetValue(model ?? string.Empty, out var found) ? found : Fallback;
        var perMillion = direction switch
        {
            "input" => r.Input,
            "output" => r.Output,
            "cache_read" => r.CacheRead,
            "cache_write" => r.CacheWrite,
            _ => 0m,
        };
        return perMillion * tokens / 1_000_000m;
    }

    private readonly record struct Rates(decimal Input, decimal Output, decimal CacheRead, decimal CacheWrite);
}
