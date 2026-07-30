using Cicd.Ai;

namespace Cicd.Web.Admin.Services.Sca;

/// <summary>
/// Explains what changed in a project's dependencies between two builds.
///
/// Synthesis tier — and it is the first structured input to warrant it. The other structured
/// features (licenses, drift) are bounded rollups where each finding stands alone. A dependency
/// diff is different in kind: it can run to hundreds of rows, and the answer worth reading is a
/// CROSS-ROW one — which upgrade dragged in the transitive package that carries the new CVE. That
/// is reasoning over a large set, which is what the Synthesis tier exists for.
///
/// Cached on the two build numbers. Both builds are settled and their Jenkins artifacts are
/// immutable, so unlike the drift explainer there is nothing here that can go stale.
/// </summary>
public sealed class SbomDiffExplainer : ISbomDiffExplainer
{
    private static readonly TimeSpan CacheFor = TimeSpan.FromDays(30);

    private const string SystemPrompt =
        "You are advising a .NET engineering team on a dependency change between two CI builds. " +
        "A diff has already been computed for you over two CycloneDX SBOMs — trust it and do not " +
        "re-derive it. Your job is to say what actually changed and whether it needs attention. " +
        "Ground every statement in the supplied diff: do NOT invent package names, versions, CVE " +
        "identifiers, release notes, or changelog entries that are not given, and do not speculate " +
        "about what a version bump contains — you are being shown version numbers, not diffs of the " +
        "packages themselves. Direct dependency changes and transitive ones are NOT distinguished " +
        "in this data, so do not assert which a component is. If the change set is routine, say so " +
        "briefly rather than inflating it. Structure as: (1) the headline — one sentence on whether " +
        "this is a routine bump or something that needs a look, (2) what changed that matters, in " +
        "priority order, (3) what to check before shipping it.";

    private readonly AiExplanationRunner _runner;

    public SbomDiffExplainer(AiExplanationRunner runner) => _runner = runner;

    public bool IsConfigured => _runner.IsConfigured;

    public async Task<SbomDiffExplanation> ExplainAsync(
        SbomDiffExplainRequest request, CancellationToken ct = default)
    {
        var outcome = await _runner.RunAsync(
            cacheKey: $"sbom-diff-explain:v1:{request.JobName}:{request.FromBuild}:{request.ToBuild}",
            feature: "explain_sbom_diff",
            tier: AiModelKind.Synthesis,
            systemPrompt: SystemPrompt,
            groundedPrompt: request.ToPromptBlock(),
            ct: ct,
            ttl: CacheFor);

        return new SbomDiffExplanation(outcome.Text, outcome.FromCache, outcome.ModelUsed);
    }
}
