namespace Cicd.Web.Admin.Services.Ai;

/// <summary>Model tier for a feature — interactive (cheap/fast) vs heavy synthesis.</summary>
public enum AiModelKind
{
    /// <summary>Latency-sensitive UI panels (CVE explain, DORA digest) — Sonnet tier.</summary>
    Interactive,

    /// <summary>Deep synthesis (deploy advisor, remediation, agentic) — Opus tier.</summary>
    Synthesis,
}

/// <summary>
/// A grounded AI request. The prompt is assembled from structured, cited data by a
/// feature-specific builder — never free-form. <see cref="Feature"/> is the metering /
/// attribution key; <see cref="Dimensions"/> carries repo/service/etc. tags for the
/// usage ledger.
/// </summary>
public sealed record AiInsightRequest(
    string Feature,
    string SystemPrompt,
    string GroundedPrompt,
    AiModelKind Model = AiModelKind.Interactive,
    IReadOnlyDictionary<string, string>? Dimensions = null);

/// <summary>The model's answer plus the usage captured at the SDK boundary.</summary>
public sealed record AiInsight(string Text, AiUsage Usage, string ModelUsed);

/// <summary>
/// Token usage read from the Anthropic response — including the cache-read/creation
/// breakdown the metering ledger needs. Captured at the SDK boundary, never inferred.
/// </summary>
public readonly record struct AiUsage(
    long InputTokens,
    long OutputTokens,
    long CacheReadInputTokens,
    long CacheCreationInputTokens)
{
    public long TotalInputTokens => InputTokens + CacheReadInputTokens + CacheCreationInputTokens;
}
