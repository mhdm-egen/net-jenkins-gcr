using Cicd.Notifications;
using Jenkins.Domain.PipelineRuns;
using Jenkins.Domain.PipelineRuns.Events;
using Microsoft.Extensions.Configuration;
using Wolverine.Attributes;

namespace Jenkins.Application.Features.Integration;

/// <summary>
/// Notification edge for orchestrator pipeline runs: in-process handlers that fan a formatted
/// message out to the configured channels (Slack / email). These run alongside the bus translators
/// in <see cref="PipelineRunTranslators"/> — same events, different sink. Best-effort: the
/// dispatcher never throws, so a notification failure can't fail a run.
///
/// These events are already rich (pipeline name, trigger, steps, failure reason), so unlike the
/// build notifiers there is nothing to re-hydrate.
///
/// [WolverineHandler] is REQUIRED — the convention only auto-discovers "*Handler"/"*Consumer" names,
/// so a "*Notifier" is invisible without it and would silently never fire.
/// </summary>
[WolverineHandler]
public sealed class PipelineRunStartedNotifier
{
    public Task Handle(
        PipelineRunStarted evt, INotificationDispatcher notify, IConfiguration config, CancellationToken ct)
        => PipelineNotificationFormat.OnStart(config)
            ? notify.NotifyAsync(new NotificationMessage(
                $"▶️ Pipeline '{evt.PipelineName}' started",
                NotificationSeverity.Info,
                new[]
                {
                    $"Triggered by: {evt.TriggeredBy}",
                    $"Run: {evt.RunId}",
                    $"At: {evt.OccurredAtUtc:u}",
                }), ct)
            : Task.CompletedTask;
}

[WolverineHandler]
public sealed class PipelineRunSucceededNotifier
{
    public Task Handle(PipelineRunSucceeded evt, INotificationDispatcher notify, CancellationToken ct)
        => notify.NotifyAsync(new NotificationMessage(
            $"✅ Pipeline '{evt.PipelineName}' succeeded",
            NotificationSeverity.Success,
            new[]
            {
                $"Steps: {PipelineNotificationFormat.Chain(evt.Steps)}",
                $"Triggered by: {evt.TriggeredBy}",
                $"Run: {evt.RunId}",
                $"At: {evt.OccurredAtUtc:u}",
            }), ct);
}

[WolverineHandler]
public sealed class PipelineRunFailedNotifier
{
    public Task Handle(PipelineRunFailed evt, INotificationDispatcher notify, CancellationToken ct)
        => notify.NotifyAsync(new NotificationMessage(
            $"❌ Pipeline '{evt.PipelineName}' failed",
            NotificationSeverity.Failure,
            new[]
            {
                // Reason first: on a failure it is the only line most readers will read.
                $"Reason: {evt.FailureReason}",
                $"Completed steps: {PipelineNotificationFormat.Chain(evt.CompletedSteps)}",
                $"Triggered by: {evt.TriggeredBy}",
                $"Run: {evt.RunId}",
                $"At: {evt.OccurredAtUtc:u}",
            }), ct);
}

[WolverineHandler]
public sealed class PipelineRunCancelledNotifier
{
    public Task Handle(PipelineRunCancelled evt, INotificationDispatcher notify, CancellationToken ct)
        => notify.NotifyAsync(new NotificationMessage(
            // Info, not Failure: a cancellation is a human decision, not a defect. Keeps
            // OnlyFailures meaningful as a genuine failures-only switch.
            $"⛔ Pipeline '{evt.PipelineName}' cancelled",
            NotificationSeverity.Info,
            new[]
            {
                $"Completed steps: {PipelineNotificationFormat.Chain(evt.Steps)}",
                $"Triggered by: {evt.TriggeredBy}",
                $"Run: {evt.RunId}",
                $"At: {evt.OccurredAtUtc:u}",
            }), ct);
}

/// <summary>
/// Shared formatting. A plain static class, deliberately: Wolverine only scans discovered types for
/// <c>Handle</c>/<c>Consume</c> methods, so this is invisible to it and cannot become a handler.
/// </summary>
internal static class PipelineNotificationFormat
{
    /// <summary>Slack renders the whole message as one text block, so a long chain is capped.</summary>
    private const int MaxSteps = 6;

    /// <summary>
    /// Start messages are separately switchable — they roughly double the volume and carry the least
    /// information. Read straight from config rather than widening the shared
    /// <see cref="NotificationOptions"/>, which deployment also binds.
    /// </summary>
    public static bool OnStart(IConfiguration config) =>
        config.GetValue("Ci:Notifications:OnStart", true);

    public static string Chain(IReadOnlyList<PipelineRunStepRecord> steps)
    {
        // The empty case is the informative one on a failure: it says the chain broke before any
        // step finished, which "0 steps" would leave the reader to infer.
        if (steps.Count == 0) return "none — failed before any step completed";

        var shown = steps.OrderBy(s => s.Order).Take(MaxSteps)
                         .Select(s => $"{s.JobName} #{s.BuildNumber}");

        return string.Join(" → ", shown) + (steps.Count > MaxSteps ? " → …" : string.Empty);
    }
}
