using Jenkins.Domain.Common;

namespace Jenkins.Domain.PipelineRuns.Events;

public sealed record PipelineRunStarted(
    Guid RunId,
    Guid PipelineId,
    string PipelineName,
    Guid? RepositoryId,
    string TriggeredBy,
    DateTimeOffset OccurredAtUtc) : IDomainEvent;

/// <summary>One step (Jenkins job) of the run completed successfully — translated to the
/// <c>Ci.PipelineStepCompleted</c> integration event.</summary>
public sealed record PipelineRunStepSucceeded(
    Guid RunId,
    Guid PipelineId,
    string PipelineName,
    Guid? RepositoryId,
    string JobName,
    int BuildNumber,
    DateTimeOffset OccurredAtUtc) : IDomainEvent;

/// <summary>The whole run completed successfully — translated to the
/// <c>Ci.PipelineCompleted</c> integration event.</summary>
public sealed record PipelineRunSucceeded(
    Guid RunId,
    Guid PipelineId,
    string PipelineName,
    Guid? RepositoryId,
    string TriggeredBy,
    IReadOnlyList<PipelineRunStepRecord> Steps,
    DateTimeOffset OccurredAtUtc) : IDomainEvent;

/// <summary>
/// The run failed — translated to the <c>Ci.PipelineFailed</c> integration event.
///
/// This event existed from the start but carried only the run id and the reason, and had no
/// translator, so failure never reached the bus at all: subscribers only ever learned about success
/// and cancellation. It now carries the same context as its siblings so a consumer can act on it
/// without calling back into the CI service, plus the steps that DID complete — which is what tells
/// a reader where in the chain it broke.
/// </summary>
public sealed record PipelineRunFailed(
    Guid RunId,
    Guid PipelineId,
    string PipelineName,
    Guid? RepositoryId,
    string TriggeredBy,
    string FailureReason,
    IReadOnlyList<PipelineRunStepRecord> CompletedSteps,
    DateTimeOffset OccurredAtUtc) : IDomainEvent;

/// <summary>The run was cancelled in flight — translated to the <c>Ci.PipelineCancelled</c>
/// integration event. Carries the steps that completed before cancellation.</summary>
public sealed record PipelineRunCancelled(
    Guid RunId,
    Guid PipelineId,
    string PipelineName,
    Guid? RepositoryId,
    string TriggeredBy,
    IReadOnlyList<PipelineRunStepRecord> Steps,
    DateTimeOffset OccurredAtUtc) : IDomainEvent;
