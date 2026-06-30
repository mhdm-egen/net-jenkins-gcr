using Deployment.Domain.AspireApps.Runs.Events;
using Wolverine.Attributes;

namespace Deployment.Application.Features.Integration;

/// <summary>
/// Producer edge (deployment → bus): translates the internal Aspire-run domain events into the
/// cross-service integration events on the "deployment.events" channel. Mirrors
/// <see cref="DeploymentRunSucceededTranslator"/> / <see cref="DeploymentRunFailedTranslator"/>.
///
/// [WolverineHandler] is REQUIRED on a "*Translator" (name doesn't end in Handler/Consumer).
/// </summary>
[WolverineHandler]
public sealed class AspireApplicationDeployedTranslator
{
    public Cicd.IntegrationEvents.Deployment.AspireApplicationDeployed Handle(AspireApplicationRunSucceeded evt)
        => new(
            EventId: Guid.NewGuid(),
            OccurredAtUtc: evt.OccurredAtUtc,
            RunId: evt.RunId,
            ApplicationId: evt.ApplicationId,
            ApplicationName: evt.ApplicationName,
            Namespace: evt.Namespace);
}

[WolverineHandler]
public sealed class AspireApplicationDeploymentFailedTranslator
{
    public Cicd.IntegrationEvents.Deployment.AspireApplicationDeploymentFailed Handle(AspireApplicationRunFailed evt)
        => new(
            EventId: Guid.NewGuid(),
            OccurredAtUtc: evt.OccurredAtUtc,
            RunId: evt.RunId,
            ApplicationId: evt.ApplicationId,
            ApplicationName: evt.ApplicationName,
            Reason: evt.Reason);
}
