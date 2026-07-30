using Cicd.Notifications;
using Deployment.Application.Features.AspireApps;
using Deployment.Application.Features.Environments;
using Deployment.Application.Features.Services;
using Deployment.Domain.AspireApps.Runs;
using Deployment.Domain.AspireApps.Runs.Events;
using Deployment.Domain.Runs;
using Deployment.Domain.Runs.Events;
using Microsoft.Extensions.Configuration;
using Wolverine.Attributes;

namespace Deployment.Application.Features.Integration;

/// <summary>
/// Notification edge for the START of a deploy. Kept apart from
/// <see cref="DeploymentNotificationHandlers"/> deliberately: those four outcome notifiers are
/// dependency-free one-liners, and mixing re-hydrating handlers into them would spoil a clean file.
///
/// **These say "queued", not "started", and that wording is load-bearing.** The event is raised in
/// the run's constructor with <c>Status = Pending</c>; <c>DeploymentRun.Start()</c> (Pending →
/// Running) raises no event at all. A run targeting a protected environment then parks at
/// <c>AwaitingApproval</c> — possibly for hours — so a message claiming it had started would be
/// wrong in exactly the case where someone is waiting on it.
///
/// Every injected dependency is service-located via <c>AlwaysUseServiceLocationFor</c> in
/// Deployment.Api's Wolverine setup. That is not optional: these notifiers join the SAME generated
/// chain as the run executor, so a codegen failure here would stop deployments outright, not just
/// notifications.
/// </summary>
[WolverineHandler]
public sealed class ServiceDeployQueuedNotifier
{
    public async Task Handle(
        DeploymentRunRequested evt,
        IDeploymentRunRepository runs,
        IServiceReader services,
        IEnvironmentReader environments,
        INotificationDispatcher notify,
        IConfiguration config,
        CancellationToken ct)
    {
        if (!DeployNotificationFormat.OnStart(config)) return;

        var run = await runs.GetByIdAsync(evt.RunId, ct);
        if (run is null) return;

        var service = await services.GetByIdAsync(evt.ServiceId, ct);
        var environment = await environments.GetByIdAsync(evt.EnvironmentId, ct);

        await notify.NotifyAsync(new NotificationMessage(
            $"▶️ Deploy queued — {service?.Name ?? run.ContainerName} → {environment?.Name ?? "unknown environment"}",
            NotificationSeverity.Info,
            new[]
            {
                $"Version: {run.Version}",
                $"Trigger: {run.Trigger}",
                $"Triggered by: {run.TriggeredBy}",
                $"Run: {run.Id}",
                $"At: {evt.OccurredAtUtc:u}",
            }), ct);
    }
}

[WolverineHandler]
public sealed class AspireDeployQueuedNotifier
{
    public async Task Handle(
        AspireApplicationRunRequested evt,
        IAspireApplicationRunRepository runs,
        IAspireApplicationReader applications,
        INotificationDispatcher notify,
        IConfiguration config,
        CancellationToken ct)
    {
        if (!DeployNotificationFormat.OnStart(config)) return;

        var run = await runs.GetByIdAsync(evt.RunId, ct);
        if (run is null) return;

        var app = await applications.GetByIdAsync(evt.ApplicationId, ct);

        await notify.NotifyAsync(new NotificationMessage(
            // EnvironmentName is on the run itself here, unlike the service side.
            $"▶️ Aspire deploy queued — {app?.Name ?? "unknown app"} → {run.EnvironmentName}",
            NotificationSeverity.Info,
            new[]
            {
                $"Version: {run.Version ?? "—"}",
                $"Triggered by: {run.TriggeredBy}",
                $"Run: {run.Id}",
                $"At: {evt.OccurredAtUtc:u}",
            }), ct);
    }
}

/// <summary>
/// A plain static class, so Wolverine — which only scans discovered types for <c>Handle</c>/
/// <c>Consume</c> methods — cannot mistake it for a handler.
/// </summary>
internal static class DeployNotificationFormat
{
    /// <summary>
    /// Start messages are separately switchable: they roughly double the volume and carry the least
    /// information. Read straight from config rather than widening the shared
    /// <see cref="NotificationOptions"/>, which the jenkins service binds from its own section.
    /// </summary>
    public static bool OnStart(IConfiguration config) =>
        config.GetValue("Deployment:Notifications:OnStart", true);
}
