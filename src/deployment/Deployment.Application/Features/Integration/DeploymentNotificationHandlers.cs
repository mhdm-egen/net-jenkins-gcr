using Cicd.Notifications;
using Deployment.Domain.AspireApps.Runs.Events;
using Deployment.Domain.Runs.Events;
using Wolverine.Attributes;

namespace Deployment.Application.Features.Integration;

/// <summary>
/// Notification edge: in-process handlers on the deploy domain events that fan a formatted message
/// out to the configured channels (Slack / email) via <see cref="INotificationDispatcher"/>. These
/// run alongside the bus translators — same events, different sink. Best-effort: the dispatcher
/// never throws, so a notification failure can't fail a deploy.
///
/// [WolverineHandler] is REQUIRED — the convention only auto-discovers "*Handler"/"*Consumer" names.
/// </summary>
[WolverineHandler]
public sealed class AspireDeploySucceededNotifier
{
    public Task Handle(AspireApplicationRunSucceeded evt, INotificationDispatcher notify, CancellationToken ct)
        => notify.NotifyAsync(new NotificationMessage(
            $"✅ Aspire app '{evt.ApplicationName}' deployed",
            NotificationSeverity.Success,
            new[]
            {
                $"Namespace: {evt.Namespace}",
                $"Run: {evt.RunId}",
                $"At: {evt.OccurredAtUtc:u}",
            }), ct);
}

[WolverineHandler]
public sealed class AspireDeployFailedNotifier
{
    public Task Handle(AspireApplicationRunFailed evt, INotificationDispatcher notify, CancellationToken ct)
        => notify.NotifyAsync(new NotificationMessage(
            $"❌ Aspire app '{evt.ApplicationName}' deploy failed",
            NotificationSeverity.Failure,
            new[]
            {
                $"Reason: {evt.Reason}",
                $"Run: {evt.RunId}",
                $"At: {evt.OccurredAtUtc:u}",
            }), ct);
}

[WolverineHandler]
public sealed class ServiceDeploySucceededNotifier
{
    public Task Handle(DeploymentRunSucceeded evt, INotificationDispatcher notify, CancellationToken ct)
        => notify.NotifyAsync(new NotificationMessage(
            $"✅ Service '{evt.ServiceName}' deployed to Cloud Run",
            NotificationSeverity.Success,
            new[]
            {
                $"Cloud Run: {evt.CloudRunServiceName} ({evt.Region})",
                $"Version: {evt.Version}",
                $"Revision: {evt.CloudRunRevision}",
                $"At: {evt.OccurredAtUtc:u}",
            }), ct);
}

[WolverineHandler]
public sealed class ServiceDeployFailedNotifier
{
    public Task Handle(DeploymentRunFailed evt, INotificationDispatcher notify, CancellationToken ct)
        => notify.NotifyAsync(new NotificationMessage(
            "❌ Service deploy failed",
            NotificationSeverity.Failure,
            new[]
            {
                $"Reason: {evt.Reason}",
                $"Step: {evt.FailedStep ?? "—"}",
                $"Run: {evt.RunId}",
                $"At: {evt.OccurredAtUtc:u}",
            }), ct);
}
