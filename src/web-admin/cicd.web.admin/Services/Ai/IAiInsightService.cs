namespace Cicd.Web.Admin.Services.Ai;

/// <summary>
/// The single entry point every AI feature calls. Behind it, one adapter
/// (<c>AiClient</c>) owns the model call, captures token usage, and hands that usage to
/// an <see cref="IAiUsageRecorder"/>. Soft-fails when unconfigured: features check
/// <see cref="IsConfigured"/> and show a banner instead of throwing.
/// </summary>
public interface IAiInsightService
{
    /// <summary>True when an API key is configured. False => features should no-op with a banner.</summary>
    bool IsConfigured { get; }

    /// <summary>Human-readable reason the service is unconfigured, or null when healthy.</summary>
    string? ConfigurationError { get; }

    /// <summary>Run one grounded request and return the model's answer + captured usage.</summary>
    Task<AiInsight> GetInsightAsync(AiInsightRequest request, CancellationToken ct = default);
}
