namespace Cicd.Web.Admin.Services.Ai;

/// <summary>
/// Configuration for the AI layer. Bound from the <c>Ai</c> section. Like
/// <see cref="Nexus.NexusOptions"/>, missing credentials do NOT fail startup — the
/// <see cref="IAiInsightService"/> records a configuration error and AI features
/// surface a banner / no-op instead.
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
    /// Model for heavy synthesis (deploy advisor, remediation, multi-step agentic).
    /// Hybrid-per-feature default: the highest-quality Opus tier.
    /// </summary>
    public string SynthesisModel { get; init; } = "claude-opus-4-8";

    /// <summary>Optional base-URL override (e.g. a gateway/proxy). Empty => Anthropic default.</summary>
    public string BaseUrl { get; init; } = string.Empty;

    /// <summary>Max output tokens per request. Responses stream, so this can be generous.</summary>
    public int MaxOutputTokens { get; init; } = 4096;

    /// <summary>Resolve the concrete model id for a feature's tier.</summary>
    public string ModelFor(AiModelKind kind) => kind switch
    {
        AiModelKind.Synthesis => SynthesisModel,
        _ => InteractiveModel,
    };
}
