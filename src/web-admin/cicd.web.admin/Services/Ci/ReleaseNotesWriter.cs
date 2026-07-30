using Cicd.Ai;

namespace Cicd.Web.Admin.Services.Ci;

public interface IReleaseNotesWriter
{
    /// <summary>False when no API key is configured — callers hide the affordance rather than fail.</summary>
    bool IsConfigured { get; }

    Task<ReleaseNotes> WriteAsync(ReleaseNotesRequest request, CancellationToken ct = default);
}

public sealed record ReleaseNotes(string Text, bool FromCache, string ModelUsed);

/// <summary>
/// Summarises what shipped across a range of builds.
///
/// Interactive tier: the input is a bounded list of one-line commit subjects that the platform
/// already assembled. There is no long log to reason backwards from and no cross-row inference of
/// the kind that pushed the SBOM diff to Synthesis — grouping a hundred subject lines by theme is
/// squarely Interactive work.
///
/// Cached on the repository and the two build numbers. A build's recorded commit never changes
/// once ingested, so a settled range is immutable.
/// </summary>
public sealed class ReleaseNotesWriter : IReleaseNotesWriter
{
    private static readonly TimeSpan CacheFor = TimeSpan.FromDays(30);

    private const string SystemPrompt =
        "You are writing release notes for a .NET team from their CI build history. You are given " +
        "one commit per build — the repository head when each build ran — and nothing else. Ground " +
        "every statement in those commit subjects: do NOT invent features, fixes, ticket numbers, " +
        "breaking changes, or migration steps that are not in the text you were given, and do not " +
        "infer what a change did beyond what its subject line says. Group related commits by theme " +
        "rather than restating the list build by build — a reader can already see the table. Be " +
        "concise and factual; this is a changelog, not marketing copy. Structure as: (1) one " +
        "sentence on what this range of builds is about, (2) the changes grouped by theme, " +
        "(3) anything a reader should be careful about, but only if the commits actually indicate " +
        "something — omit the section entirely rather than manufacturing a caveat.";

    private readonly AiExplanationRunner _runner;

    public ReleaseNotesWriter(AiExplanationRunner runner) => _runner = runner;

    public bool IsConfigured => _runner.IsConfigured;

    public async Task<ReleaseNotes> WriteAsync(ReleaseNotesRequest request, CancellationToken ct = default)
    {
        var outcome = await _runner.RunAsync(
            cacheKey: $"release-notes:v1:{request.RepositoryName}:{request.FromBuild}:{request.ToBuild}",
            feature: "release_notes",
            tier: AiModelKind.Interactive,
            systemPrompt: SystemPrompt,
            groundedPrompt: request.ToPromptBlock(),
            ttl: CacheFor,
            ct: ct);

        return new ReleaseNotes(outcome.Text, outcome.FromCache, outcome.ModelUsed);
    }
}
