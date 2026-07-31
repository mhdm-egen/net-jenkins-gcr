namespace Cicd.Web.Admin.Services.Ci;

/// <summary>
/// Explains why a pipeline run failed, grounded in the run's persisted console output and step
/// record, and cached. Soft-fails: <see cref="IsConfigured"/> is false when no AI key is set, and
/// the UI hides the action rather than erroring. Mirrors <see cref="Sca.ICveExplainer"/>.
/// </summary>
public interface IPipelineFailureExplainer
{
    bool IsConfigured { get; }

    Task<PipelineFailureExplanation> ExplainAsync(
        PipelineFailureExplainRequest request, CancellationToken ct = default);
}
