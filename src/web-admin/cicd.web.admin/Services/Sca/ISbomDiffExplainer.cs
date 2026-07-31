namespace Cicd.Web.Admin.Services.Sca;

public interface ISbomDiffExplainer
{
    /// <summary>False when no API key is configured — callers hide the affordance rather than fail.</summary>
    bool IsConfigured { get; }

    Task<SbomDiffExplanation> ExplainAsync(SbomDiffExplainRequest request, CancellationToken ct = default);
}

public sealed record SbomDiffExplanation(string Text, bool FromCache, string ModelUsed);
