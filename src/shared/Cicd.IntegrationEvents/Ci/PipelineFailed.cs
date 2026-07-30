namespace Cicd.IntegrationEvents.Ci;

/// <summary>
/// An orchestration pipeline run failed. Emitted by the CI service once per failed run.
///
/// Until this existed the bus only ever learned about success and cancellation, which made
/// anything failure-driven — alerting, a failure digest, a change-failure meter — impossible
/// without polling the CI service. That was recorded as a blocker in the AI roadmap; this is the
/// event that clears it.
///
/// <see cref="CompletedSteps"/> carries the steps that DID finish, so a consumer can tell where in
/// the chain the run broke without calling back. <see cref="FailureReason"/> is the run's own
/// recorded reason, never a placeholder.
/// </summary>
public sealed record PipelineFailed(
    Guid EventId,
    DateTimeOffset OccurredAtUtc,
    Guid RunId,
    Guid PipelineId,
    string PipelineName,
    Guid? RepositoryId,
    string TriggeredBy,
    string FailureReason,
    IReadOnlyList<PipelineCompletedStep> CompletedSteps) : IIntegrationEvent;
