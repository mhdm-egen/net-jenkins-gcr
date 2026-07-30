namespace Cicd.Ai;

/// <summary>
/// Configuration for the AI layer. Bound from the <c>Ai</c> section. Missing credentials do
/// NOT fail startup — the <see cref="IAiInsightService"/> records a configuration error and
/// AI features hide themselves / no-op instead.
/// </summary>
public sealed record AiOptions
{
    public const string SectionName = "Ai";

    /// <summary>
    /// Anthropic API key. NEVER commit this — supply via env var <c>Ai__ApiKey</c>
    /// (double underscore) or a docker-compose secret. Empty => AI features disabled.
    /// </summary>
    public string ApiKey { get; init; } = string.Empty;

    /// <summary>
    /// Model for interactive, latency-sensitive panels (CVE explain, DORA digest).
    /// Hybrid-per-feature default: the cheaper/faster Sonnet tier.
    /// </summary>
    public string InteractiveModel { get; init; } = "claude-sonnet-5";

    /// <summary>
    /// Model for heavy synthesis (pipeline-failure triage, deploy advisor, remediation).
    /// Hybrid-per-feature default: the highest-quality Opus tier.
    /// NOTE: changing this needs a matching row in the metering service's <c>UsageRater</c> table,
    /// or the cost falls through to the Opus-tier default rate.
    /// </summary>
    public string SynthesisModel { get; init; } = "claude-opus-5";

    /// <summary>Optional base-URL override (e.g. a gateway/proxy). Empty => Anthropic default.</summary>
    public string BaseUrl { get; init; } = string.Empty;

    /// <summary>
    /// Max output tokens per request. Calls are NOT streamed, so raising this much risks an HTTP
    /// timeout — switch <c>AiClient</c> to the streaming API first if a feature needs more.
    /// </summary>
    public int MaxOutputTokens { get; init; } = 4096;

    /// <summary>Resolve the concrete model id for a feature's tier.</summary>
    public string ModelFor(AiModelKind kind) => kind switch
    {
        AiModelKind.Synthesis => SynthesisModel,
        _ => InteractiveModel,
    };
}
