using Jenkins.Contracts.PipelineRuns;

namespace Cicd.Web.Admin.Services.Ci;

/// <summary>
/// Everything the pipeline-failure prompt is grounded in — assembled from a settled
/// <c>PipelineRunDto</c> plus the persisted console output. No free-form text: the model
/// only sees these cited facts. Mirrors <see cref="Sca.CveExplainRequest"/>.
/// </summary>
/// <param name="ConsoleTail">
/// The TAIL of the failing job's console output, not the whole log (see
/// <see cref="ConsoleTailChars"/>). The prompt says so explicitly so the model doesn't read the
/// first line as the start of the run.
/// </param>
public sealed record PipelineFailureExplainRequest(
    Guid RunId,
    string PipelineName,
    string? Branch,
    string TriggeredBy,
    string? FailureReason,
    IReadOnlyList<string> SucceededSteps,
    string? FailingJobName,
    string ConsoleTail,
    Guid? RepositoryId = null)
{
    /// <summary>
    /// How much of the failing job's console to send. A CI console can be megabytes; the failure
    /// is at the end, so we take the tail. 12k chars sits under the 16k tail-trim that
    /// <c>AspireApplicationRun.Log</c> already uses for the same reason.
    /// </summary>
    public const int ConsoleTailChars = 12_000;

    /// <summary>
    /// Builds a grounded request from a settled run and its persisted console segments.
    /// Kept here rather than in the page so the failing-job heuristic is testable and so slice-2
    /// surfaces can reuse it.
    /// </summary>
    public static PipelineFailureExplainRequest FromRun(
        PipelineRunDto run, IReadOnlyList<PipelineRunConsoleDto> consoleSegments)
    {
        // Only SUCCEEDED steps are ever appended to a run (RecordStepSucceeded), so the failing
        // job is the last console segment that isn't among them. Falls back to the last segment,
        // which covers a run that failed before any step settled.
        var succeeded = run.Steps
            .Where(s => s.Result.Equals("Success", StringComparison.OrdinalIgnoreCase))
            .Select(s => s.JobName)
            .ToList();
        var succeededSet = succeeded.ToHashSet(StringComparer.OrdinalIgnoreCase);

        var failing = consoleSegments.LastOrDefault(c => !succeededSet.Contains(c.JobName))
                      ?? consoleSegments.LastOrDefault();

        return new PipelineFailureExplainRequest(
            RunId: run.Id,
            PipelineName: run.PipelineName,
            Branch: run.Branch,
            TriggeredBy: run.TriggeredBy,
            FailureReason: run.FailureReason,
            SucceededSteps: succeeded,
            FailingJobName: failing?.JobName,
            ConsoleTail: Tail(failing?.Content),
            RepositoryId: run.RepositoryId);
    }

    private static string Tail(string? content)
    {
        if (string.IsNullOrWhiteSpace(content)) return string.Empty;
        return content.Length <= ConsoleTailChars
            ? content
            : content[^ConsoleTailChars..];
    }
}

/// <summary>The generated triage plus provenance (cache hit / model used).</summary>
public sealed record PipelineFailureExplanation(string Text, bool FromCache, string ModelUsed);
