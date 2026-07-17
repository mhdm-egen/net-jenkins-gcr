namespace Jenkins.Domain.PipelineRuns;

/// <summary>
/// Persisted console output for a completed pipeline run, one row per (run, job). Written at settle time from the
/// in-memory <c>PipelineRunConsoleBuffer</c> (which is live-only) so a finished run's logs remain readable over REST.
/// A plain entity — no domain events — stored in its own table because the content is large.
/// </summary>
public sealed class PipelineRunConsoleLog
{
    public Guid Id { get; private set; }
    public Guid RunId { get; private set; }
    public string JobName { get; private set; }
    public int BuildNumber { get; private set; }
    public string Content { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }

    private PipelineRunConsoleLog()
    {
        JobName = string.Empty;
        Content = string.Empty;
    }

    public PipelineRunConsoleLog(Guid id, Guid runId, string jobName, int buildNumber, string content, DateTimeOffset updatedAtUtc)
    {
        Id = id;
        RunId = runId;
        JobName = jobName ?? string.Empty;
        BuildNumber = buildNumber;
        Content = content ?? string.Empty;
        UpdatedAtUtc = updatedAtUtc;
    }
}
