namespace Cicd.Web.Admin.Services.Sca;

/// <summary>
/// Everything the CVE-explain prompt is grounded in — assembled from a parsed
/// <c>SbomVulnerability</c> plus the resolved affected component(s). No free-form text:
/// the model only sees these cited facts.
/// </summary>
public sealed record CveExplainRequest(
    string CveId,
    string? Severity,
    string? Source,
    string? Description,
    IReadOnlyList<string> AffectedComponents,   // "Name @ version"
    IReadOnlyList<string> ReferenceUrls);

/// <summary>The generated explanation plus provenance (cache hit / model used).</summary>
public sealed record CveExplanation(string Text, bool FromCache, string ModelUsed);
