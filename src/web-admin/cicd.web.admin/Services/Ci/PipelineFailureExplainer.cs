using System.Text;
using Cicd.Ai;

namespace Cicd.Web.Admin.Services.Ci;

/// <summary>
/// Grounded, cached triage of a failed pipeline run. Builds a cited prompt from the run's step
/// record and the tail of the failing job's console, runs it on the SYNTHESIS model tier via
/// <see cref="IAiInsightService"/> (usage flows to the metering ledger automatically), and caches
/// the answer in Redis keyed by run id — a settled run is immutable, so re-opening costs nothing.
///
/// Synthesis rather than Interactive: reasoning backwards from a long, noisy build log to a cause
/// is exactly the "deep synthesis" case that tier was defined for. This is its first caller.
/// </summary>
public sealed class PipelineFailureExplainer : IPipelineFailureExplainer
{
    /// <summary>A settled run is immutable, so its triage stays valid for as long as we keep it.</summary>
    private static readonly TimeSpan CacheFor = TimeSpan.FromDays(7);

    private const string SystemPrompt =
        "You are a CI/CD engineer helping a .NET developer triage a failed Jenkins pipeline run. " +
        "Ground every statement in the supplied run record and console output — do NOT invent file " +
        "paths, package names, versions, commands, or error text that are not present in the data. " +
        "The console is a TAIL of one job's output, so earlier context may be missing; if the visible " +
        "output does not explain the failure, say so plainly rather than guessing. Be concise and " +
        "practical, and prefer the specific over the generic: quote the actual error line when you " +
        "can see it. Structure the answer as: (1) what failed, (2) the most likely cause, " +
        "(3) what to try first, (4) anything the log does not show that would confirm the diagnosis.";

    private readonly AiExplanationRunner _runner;

    public PipelineFailureExplainer(AiExplanationRunner runner) => _runner = runner;

    public bool IsConfigured => _runner.IsConfigured;

    public async Task<PipelineFailureExplanation> ExplainAsync(
        PipelineFailureExplainRequest request, CancellationToken ct = default)
    {
        // Attribute the spend to the repository so per-repo showback works in the ledger.
        var dimensions = request.RepositoryId is { } repoId
            ? new Dictionary<string, string> { ["repository"] = repoId.ToString() }
            : null;

        var outcome = await _runner.RunAsync(
            cacheKey: $"pipeline-explain:v1:{request.RunId}",
            feature: "explain_pipeline_failure",
            tier: AiModelKind.Synthesis,
            systemPrompt: SystemPrompt,
            groundedPrompt: BuildPrompt(request),
            ttl: CacheFor,
            dimensions: dimensions,
            ct: ct);

        return new PipelineFailureExplanation(outcome.Text, outcome.FromCache, outcome.ModelUsed);
    }

    private static string BuildPrompt(PipelineFailureExplainRequest r)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Pipeline: {r.PipelineName}");
        sb.AppendLine($"Branch: {(string.IsNullOrEmpty(r.Branch) ? "(not recorded)" : r.Branch)}");
        sb.AppendLine($"Triggered by: {r.TriggeredBy}");
        sb.AppendLine($"Failure reason recorded by the orchestrator: " +
                      $"{(string.IsNullOrWhiteSpace(r.FailureReason) ? "(none)" : r.FailureReason)}");
        sb.AppendLine();

        sb.AppendLine("Jobs that completed successfully before the failure, in order:");
        if (r.SucceededSteps.Count == 0)
            sb.AppendLine("  (none — the run failed on its first job)");
        else
            foreach (var s in r.SucceededSteps) sb.AppendLine($"  - {s}");
        sb.AppendLine();

        sb.AppendLine($"Failing job: {(string.IsNullOrEmpty(r.FailingJobName) ? "(could not be determined)" : r.FailingJobName)}");
        sb.AppendLine();

        if (string.IsNullOrWhiteSpace(r.ConsoleTail))
        {
            sb.AppendLine("No console output was captured for the failing job. Say so, and base your " +
                          "answer only on the failure reason and the step list above.");
        }
        else
        {
            sb.AppendLine($"Tail of the failing job's console output (the LAST {PipelineFailureExplainRequest.ConsoleTailChars} " +
                          "characters at most — earlier output is not shown):");
            sb.AppendLine("```");
            sb.AppendLine(r.ConsoleTail);
            sb.AppendLine("```");
        }

        return sb.ToString();
    }
}
