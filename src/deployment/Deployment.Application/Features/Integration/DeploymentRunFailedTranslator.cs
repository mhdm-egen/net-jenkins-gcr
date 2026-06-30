using Deployment.Domain.Runs.Events;
using Wolverine.Attributes;

namespace Deployment.Application.Features.Integration;

/// <summary>
/// Producer edge (deployment → bus): translates the internal <see cref="DeploymentRunFailed"/>
/// domain event into the cross-service <see cref="Cicd.IntegrationEvents.Deployment.ServiceDeploymentFailed"/>
/// integration event. The cascaded return is published through the SQL outbox onto the
/// "deployment.events" channel — the failure counterpart to <see cref="DeploymentRunSucceededTranslator"/>.
///
/// [WolverineHandler] is REQUIRED: Wolverine's convention only auto-discovers types whose names end
/// in "Handler"/"Consumer", so a "*Translator" is invisible without it (and the integration event is
/// never published).
/// </summary>
[WolverineHandler]
public sealed class DeploymentRunFailedTranslator
{
    public Cicd.IntegrationEvents.Deployment.ServiceDeploymentFailed Handle(DeploymentRunFailed evt)
        => new(
            EventId: Guid.NewGuid(),
            OccurredAtUtc: evt.OccurredAtUtc,
            RunId: evt.RunId,
            ServiceId: evt.ServiceId,
            EnvironmentId: evt.EnvironmentId,
            Reason: evt.Reason,
            FailedStep: evt.FailedStep,
            Category: evt.Category);
}
