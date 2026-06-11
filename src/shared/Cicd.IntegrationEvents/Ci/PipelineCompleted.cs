namespace Cicd.IntegrationEvents.Ci;

/// <summary>
/// An orchestration pipeline run completed successfully (all steps green). Emitted by the CI
/// service once per successful run; downstream services (deployment, publishing, notifications)
/// may react.
/// </summary>
public sealed record PipelineCompleted(
    Guid EventId,
    DateTimeOffset OccurredAtUtc,
    Guid RunId,
    Guid PipelineId,
    string PipelineName,
    Guid? RepositoryId,
    string TriggeredBy,
    IReadOnlyList<PipelineCompletedStep> Steps) : IIntegrationEvent;

public sealed record PipelineCompletedStep(string JobName, int BuildNumber);
