using Cicd.Notifications;
using Deployment.Application.Features.Environments;
using Deployment.Application.Features.Services;
using Deployment.Domain.AspireApps.Runs.Events;
using Deployment.Domain.Runs;
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

/// <summary>
/// The only notifier here that takes dependencies, and the only one that needs to.
///
/// <c>DeploymentRunFailed</c> carries Guids where <c>DeploymentRunSucceeded</c> carries a
/// ServiceName, so this used to render the bare title "❌ Service deploy failed" — no service, no
/// environment, no version. That is a papercut on the single most important message in the set: the
/// one you get at 2am. The readers needed to fix it are already service-located for the
/// deploy-queued notifiers, so resolving the names here costs two lookups and no new wiring.
/// </summary>
[WolverineHandler]
public sealed class ServiceDeployFailedNotifier
{
    public async Task Handle(
        DeploymentRunFailed evt,
        IDeploymentRunRepository runs,
        IServiceReader services,
        IEnvironmentReader environments,
        INotificationDispatcher notify,
        CancellationToken ct)
    {
        var run = await runs.GetByIdAsync(evt.RunId, ct);
        var service = await services.GetByIdAsync(evt.ServiceId, ct);
        var environment = await environments.GetByIdAsync(evt.EnvironmentId, ct);

        // Fall back to the container name, then to the old wording — a lookup miss must still
        // produce a message, because this is the failure path.
        var target = service?.Name ?? run?.ContainerName;
        var title = target is null
            ? "❌ Service deploy failed"
            : $"❌ Deploy failed — {target} → {environment?.Name ?? "unknown environment"}";

        var fields = new List<string> { $"Reason: {evt.Reason}" };

        // Category is the typed StepFailureKind ("registry auth", …) — the fastest route to a
        // diagnosis, so it sits next to the step rather than at the bottom.
        fields.Add(evt.Category is { Length: > 0 } category
            ? $"Step: {evt.FailedStep ?? "—"} ({category})"
            : $"Step: {evt.FailedStep ?? "—"}");

        if (run is not null) fields.Add($"Version: {run.Version}");
        fields.Add($"Run: {evt.RunId}");
        fields.Add($"At: {evt.OccurredAtUtc:u}");

        await notify.NotifyAsync(
            new NotificationMessage(title, NotificationSeverity.Failure, fields), ct);
    }
}
