using Deployment.Application.Abstractions;
using Deployment.Domain.AspireApps.Runs.Events;
using Wolverine.Attributes;

namespace Deployment.Application.Features.Integration;

/// <summary>
/// When an Aspire-app run settles, broadcast an app-wide completion toast over the same SignalR channel
/// the per-service runs use (<see cref="IDeploymentRunNotifier"/> → "DeploymentCompleted"), so the
/// web-admin pops a toast on any page. Mirrors <c>DeploymentRunCompletedNotifier</c>.
///
/// [WolverineHandler] is REQUIRED: a "*Notifier" isn't auto-discovered by name.
/// </summary>
[WolverineHandler]
public sealed class AspireApplicationRunCompletedNotifier
{
    public Task Handle(AspireApplicationRunSucceeded e, IDeploymentRunNotifier notifier, CancellationToken ct)
        => notifier.RunCompletedAsync(
            e.RunId,
            "Succeeded",
            $"Deployed {e.ApplicationName} to Kubernetes",
            e.Namespace,
            ct);

    public Task Handle(AspireApplicationRunFailed e, IDeploymentRunNotifier notifier, CancellationToken ct)
        => notifier.RunCompletedAsync(
            e.RunId,
            "Failed",
            $"Aspire deploy failed — {e.ApplicationName}",
            e.Reason,
            ct);
}
