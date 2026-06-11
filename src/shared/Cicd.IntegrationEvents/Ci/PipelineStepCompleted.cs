namespace Cicd.IntegrationEvents.Ci;

/// <summary>
/// A single step (Jenkins job) of an orchestration pipeline run completed successfully.
/// Emitted by the CI service per successful step.
/// </summary>
public sealed record PipelineStepCompleted(
    Guid EventId,
    DateTimeOffset OccurredAtUtc,
    Guid RunId,
    Guid PipelineId,
    string PipelineName,
    string JobName,
    int BuildNumber,
    Guid? RepositoryId) : IIntegrationEvent;
