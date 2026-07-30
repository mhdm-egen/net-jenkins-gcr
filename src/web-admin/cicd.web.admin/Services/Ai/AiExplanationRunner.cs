using Microsoft.Extensions.Caching.Distributed;

namespace Cicd.Web.Admin.Services.Ai;

/// <summary>The result of a cached explanation — text plus where it came from.</summary>
public sealed record AiExplanationOutcome(string Text, bool FromCache, string ModelUsed);

/// <summary>
/// The cache-then-call half that every AI explanation feature shares: look in the distributed
/// cache, otherwise run one grounded request through <see cref="IAiInsightService"/> and cache the
/// answer. Features keep the part that is actually theirs — the prompt, the tier, the cache key —
/// and stop re-implementing the plumbing around it.
///
/// Extracted at the third caller rather than the first. Empty answers are deliberately NOT cached,
/// so a transient empty response doesn't pin itself for the whole TTL.
/// </summary>
public sealed class AiExplanationRunner
{
    private readonly IAiInsightService _ai;
    private readonly IDistributedCache _cache;

    public AiExplanationRunner(IAiInsightService ai, IDistributedCache cache)
    {
        _ai = ai;
        _cache = cache;
    }

    public bool IsConfigured => _ai.IsConfigured;

    public async Task<AiExplanationOutcome> RunAsync(
        string cacheKey,
        string feature,
        AiModelKind tier,
        string systemPrompt,
        string groundedPrompt,
        TimeSpan ttl,
        IReadOnlyDictionary<string, string>? dimensions = null,
        CancellationToken ct = default)
    {
        var cached = await _cache.GetStringAsync(cacheKey, ct);
        if (cached is { Length: > 0 })
            return new AiExplanationOutcome(cached, FromCache: true, ModelUsed: "cache");

        var insight = await _ai.GetInsightAsync(new AiInsightRequest(
            Feature: feature,
            SystemPrompt: systemPrompt,
            GroundedPrompt: groundedPrompt,
            Model: tier,
            Dimensions: dimensions), ct);

        if (!string.IsNullOrWhiteSpace(insight.Text))
            await _cache.SetStringAsync(cacheKey, insight.Text, new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = ttl,
            }, ct);

        return new AiExplanationOutcome(insight.Text, FromCache: false, ModelUsed: insight.ModelUsed);
    }
}
