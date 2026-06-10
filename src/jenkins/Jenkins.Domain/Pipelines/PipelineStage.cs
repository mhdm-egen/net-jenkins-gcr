using Jenkins.Domain.Common;

namespace Jenkins.Domain.Pipelines;

/// <summary>
/// One stage of a <see cref="Pipeline"/> — a Jenkins job to run, its position in
/// the sequence, an optional upstream job whose build number is forwarded as
/// <c>SOURCE_BUILD_NUMBER</c>, and per-stage build parameters. Maps 1:1 onto the
/// orchestrator's <c>PipelineStep</c> at run time.
///
/// Child entity of <see cref="Pipeline"/>; created/mutated only via the root.
/// </summary>
public sealed class PipelineStage : Entity<Guid>
{
    public Guid PipelineId { get; private set; }
    public int Order { get; private set; }
    public string JobName { get; private set; }
    public string? UpstreamJobName { get; private set; }

    // Not readonly: EF assigns it on materialization via the parameters JSON converter.
    private Dictionary<string, string> _parameters = new();
    public IReadOnlyDictionary<string, string> Parameters => _parameters;

    private PipelineStage()
    {
        JobName = string.Empty;
    }

    internal PipelineStage(
        Guid id,
        Guid pipelineId,
        int order,
        string jobName,
        string? upstreamJobName,
        IReadOnlyDictionary<string, string>? parameters)
    {
        if (id == Guid.Empty) throw new ArgumentException("Id cannot be empty.", nameof(id));
        if (pipelineId == Guid.Empty)
            throw new ArgumentException("PipelineId cannot be empty.", nameof(pipelineId));
        if (string.IsNullOrWhiteSpace(jobName))
            throw new ArgumentException("JobName cannot be empty.", nameof(jobName));

        Id = id;
        PipelineId = pipelineId;
        Order = order;
        JobName = jobName.Trim();
        UpstreamJobName = Normalize(upstreamJobName);
        _parameters = Copy(parameters);
    }

    internal void Update(string jobName, string? upstreamJobName, IReadOnlyDictionary<string, string>? parameters)
    {
        if (string.IsNullOrWhiteSpace(jobName))
            throw new ArgumentException("JobName cannot be empty.", nameof(jobName));
        JobName = jobName.Trim();
        UpstreamJobName = Normalize(upstreamJobName);
        _parameters = Copy(parameters);
    }

    internal void SetOrder(int order) => Order = order;

    private static string? Normalize(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    private static Dictionary<string, string> Copy(IReadOnlyDictionary<string, string>? p) =>
        p is null ? new Dictionary<string, string>() : new Dictionary<string, string>(p);
}
