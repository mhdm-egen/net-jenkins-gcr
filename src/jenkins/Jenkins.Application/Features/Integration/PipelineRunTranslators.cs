using Jenkins.Domain.PipelineRuns.Events;
using Wolverine.Attributes;

namespace Jenkins.Application.Features.Integration;

/// <summary>
/// Translation edge (CI → bus): a successful pipeline step → the cross-service
/// <see cref="Cicd.IntegrationEvents.Ci.PipelineStepCompleted"/> integration event (cascaded
/// onto "ci.events" via the outbox).
/// </summary>
[WolverineHandler]
public sealed class PipelineRunStepSucceededTranslator
{
    public Cicd.IntegrationEvents.Ci.PipelineStepCompleted Handle(PipelineRunStepSucceeded evt)
        => new(
            EventId: Guid.NewGuid(),
            OccurredAtUtc: evt.OccurredAtUtc,
            RunId: evt.RunId,
            PipelineId: evt.PipelineId,
            PipelineName: evt.PipelineName,
            JobName: evt.JobName,
            BuildNumber: evt.BuildNumber,
            RepositoryId: evt.RepositoryId);
}

/// <summary>
/// Translation edge (CI → bus): a successful whole pipeline run → the cross-service
/// <see cref="Cicd.IntegrationEvents.Ci.PipelineCompleted"/> integration event.
/// </summary>
[WolverineHandler]
public sealed class PipelineRunSucceededTranslator
{
    public Cicd.IntegrationEvents.Ci.PipelineCompleted Handle(PipelineRunSucceeded evt)
        => new(
            EventId: Guid.NewGuid(),
            OccurredAtUtc: evt.OccurredAtUtc,
            RunId: evt.RunId,
            PipelineId: evt.PipelineId,
            PipelineName: evt.PipelineName,
            RepositoryId: evt.RepositoryId,
            TriggeredBy: evt.TriggeredBy,
            Steps: evt.Steps.Select(s => new Cicd.IntegrationEvents.Ci.PipelineCompletedStep(s.JobName, s.BuildNumber)).ToList());
}

/// <summary>
/// Translation edge (CI → bus): a cancelled pipeline run → the cross-service
/// <see cref="Cicd.IntegrationEvents.Ci.PipelineCancelled"/> integration event.
/// </summary>
[WolverineHandler]
public sealed class PipelineRunCancelledTranslator
{
    public Cicd.IntegrationEvents.Ci.PipelineCancelled Handle(PipelineRunCancelled evt)
        => new(
            EventId: Guid.NewGuid(),
            OccurredAtUtc: evt.OccurredAtUtc,
            RunId: evt.RunId,
            PipelineId: evt.PipelineId,
            PipelineName: evt.PipelineName,
            RepositoryId: evt.RepositoryId,
            TriggeredBy: evt.TriggeredBy,
            CompletedSteps: evt.Steps.Select(s => new Cicd.IntegrationEvents.Ci.PipelineCompletedStep(s.JobName, s.BuildNumber)).ToList());
}
