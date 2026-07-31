namespace Cicd.Web.Admin.Services.Sca;

/// <summary>The generated license assessment plus provenance (cache hit / model used).</summary>
public sealed record LicenseExplanation(string Text, bool FromCache, string ModelUsed);

/// <summary>
/// Grounding for a license assessment. Assembled from <see cref="LicenseAnalyzer"/> output, which has
/// already done the categorisation and written human-readable reasons — the model's job is the
/// "can we ship this" rollup and prioritisation, not the analysis.
/// </summary>
public sealed record LicenseExplainRequest(
    string Subject,
    string RootCategory,
    int TotalComponents,
    LicenseCategoryCounts Counts,
    IReadOnlyList<LicenseFinding> Conflicts)
{
    /// <summary>
    /// Cache key input. Keyed by the SHAPE of the analysis rather than the build number, so two builds
    /// with identical license posture share one answer — which is the common case across rebuilds of
    /// the same dependency set.
    /// </summary>
    public string Fingerprint()
    {
        var conflicts = string.Join("|", Conflicts
            .Select(c => $"{c.Severity}:{c.Category}:{c.Component}")
            .OrderBy(x => x, StringComparer.Ordinal));
        return $"{RootCategory}:{TotalComponents}:{Counts.Permissive}/{Counts.WeakCopyleft}/" +
               $"{Counts.StrongCopyleft}/{Counts.NetworkCopyleft}/{Counts.PublicDomain}/" +
               $"{Counts.Proprietary}/{Counts.Unknown}:{conflicts}";
    }

    public static LicenseExplainRequest FromAnalysis(
        string subject, DependencyGraph graph, LicenseAnalysis analysis)
    {
        var nameByRef = graph.Nodes.ToDictionary(
            n => n.Ref,
            n => string.IsNullOrWhiteSpace(n.Version) ? n.Name : $"{n.Name} {n.Version}",
            StringComparer.Ordinal);

        return new LicenseExplainRequest(
            Subject: subject,
            RootCategory: LicenseAnalyzer.Display(analysis.RootCategory),
            TotalComponents: graph.Nodes.Count,
            Counts: analysis.Counts,
            Conflicts: analysis.Conflicts
                .Select(c => new LicenseFinding(
                    Component: nameByRef.TryGetValue(c.SourceRef, out var n) ? n : c.SourceRef,
                    Category: LicenseAnalyzer.Display(c.SourceCategory),
                    Severity: c.Severity.ToString(),
                    Reason: c.Reason))
                .ToList());
    }
}

/// <summary>One flagged license finding as the prompt sees it.</summary>
public sealed record LicenseFinding(string Component, string Category, string Severity, string Reason);
