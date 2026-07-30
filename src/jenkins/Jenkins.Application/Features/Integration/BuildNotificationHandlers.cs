using Cicd.Notifications;
using Jenkins.Domain.Builds;
using Jenkins.Domain.Builds.Events;
using Jenkins.Domain.SourceRepositories;
using Microsoft.Extensions.Configuration;
using Wolverine.Attributes;

namespace Jenkins.Application.Features.Integration;

/// <summary>
/// Notification edge for CI builds. Until this existed, <c>BuildFailed</c> and <c>BuildAborted</c>
/// had no subscriber at all — a failed build was invisible to every part of the platform except the
/// Builds page.
///
/// The terminal events carry only a BuildId and timestamps, so these re-hydrate the aggregate — the
/// same thing <c>AutoPublishHandler</c> already does on this very chain, which is also why the
/// stores injected here are known-safe for Wolverine's codegen.
///
/// **`BuildSucceeded` is a shared chain.** AutoPublishHandler → handoffs → releases → deployments
/// lives on it too, and Wolverine composes every handler for a message type into one generated
/// class. A codegen failure here would stop auto-publish silently. INotificationDispatcher is
/// hatched with AlwaysUseServiceLocationFor in Jenkins.Api for that reason.
///
/// [WolverineHandler] is REQUIRED — the convention only auto-discovers "*Handler"/"*Consumer".
/// </summary>
[WolverineHandler]
public sealed class BuildStartedNotifier
{
    public Task Handle(
        BuildStarted evt,
        IBuildStore builds,
        ISourceRepositoryStore repositories,
        INotificationDispatcher notify,
        IConfiguration config,
        TimeProvider clock,
        CancellationToken ct)
        => BuildNotificationFormat.OnStart(config)
            ? BuildNotificationFormat.SendStartAsync(evt, builds, repositories, notify, clock, ct)
            : Task.CompletedTask;
}

[WolverineHandler]
public sealed class BuildSucceededNotifier
{
    public Task Handle(
        BuildSucceeded evt, IBuildStore builds, ISourceRepositoryStore repositories,
        INotificationDispatcher notify, TimeProvider clock, CancellationToken ct)
        => BuildNotificationFormat.SendOutcomeAsync(
            evt.BuildId, evt.CompletedAtUtc, "✅", "succeeded",
            NotificationSeverity.Success, builds, repositories, notify, clock, ct);
}

[WolverineHandler]
public sealed class BuildFailedNotifier
{
    public Task Handle(
        BuildFailed evt, IBuildStore builds, ISourceRepositoryStore repositories,
        INotificationDispatcher notify, TimeProvider clock, CancellationToken ct)
        => BuildNotificationFormat.SendOutcomeAsync(
            evt.BuildId, evt.CompletedAtUtc, "❌", "failed",
            NotificationSeverity.Failure, builds, repositories, notify, clock, ct);
}

[WolverineHandler]
public sealed class BuildAbortedNotifier
{
    public Task Handle(
        BuildAborted evt, IBuildStore builds, ISourceRepositoryStore repositories,
        INotificationDispatcher notify, TimeProvider clock, CancellationToken ct)
        // Info, not Failure: an abort is a human decision, not a defect.
        => BuildNotificationFormat.SendOutcomeAsync(
            evt.BuildId, evt.CompletedAtUtc, "⛔", "aborted",
            NotificationSeverity.Info, builds, repositories, notify, clock, ct);
}

/// <summary>
/// Shared formatting + the staleness guard. A plain static class, so Wolverine — which only scans
/// discovered types for <c>Handle</c>/<c>Consume</c> methods — cannot mistake it for a handler.
/// </summary>
internal static class BuildNotificationFormat
{
    /// <summary>
    /// Ignore builds whose event timestamp is older than this.
    ///
    /// The sync service backfills up to <c>BackfillCount</c> (25) historical builds on first run and
    /// after a CI-history reset, recording each as Running and settling it moments later — which
    /// without this guard is a burst of ~50 messages about builds that ran days ago. Slack throttles
    /// around 1/sec and the sender has no retry, so most would be dropped with a warning anyway.
    ///
    /// A live build is noticed within PollIntervalSeconds (30s), so 30 minutes is a wide margin.
    /// Accepted trade-off: if jenkins-api is down for an hour, real builds from that window go
    /// unannounced. A constant rather than an options binding on purpose — injecting
    /// IOptionsMonitor&lt;T&gt; into a Wolverine handler is the generic-construction shape that
    /// trips codegen, and this is one number.
    /// </summary>
    private static readonly TimeSpan MaxAge = TimeSpan.FromMinutes(30);

    private const int MaxCommitMessageChars = 80;

    public static bool OnStart(IConfiguration config) =>
        config.GetValue("Ci:Notifications:OnStart", true);

    public static async Task SendStartAsync(
        BuildStarted evt, IBuildStore builds, ISourceRepositoryStore repositories,
        INotificationDispatcher notify, TimeProvider clock, CancellationToken ct)
    {
        if (IsStale(clock, evt.StartedAtUtc)) return;

        var build = await builds.GetByIdAsync(evt.BuildId, ct);
        if (build is null) return;

        var name = await RepositoryNameAsync(repositories, build, ct);

        // Shorter than the outcome message on purpose: version, duration and result don't exist yet.
        await notify.NotifyAsync(new NotificationMessage(
            $"▶️ Build started — {name} #{build.CiBuildNumber}",
            NotificationSeverity.Info,
            new[]
            {
                $"Job: {build.CiJobName} #{build.CiBuildNumber}",
                $"Branch: {build.SourceRevision.Branch}",
                $"Commit: {Commit(build)}",
                $"At: {evt.StartedAtUtc:u}",
            },
            Link: build.CiRunUrl), ct);
    }

    public static async Task SendOutcomeAsync(
        Guid buildId, DateTimeOffset completedAtUtc, string icon, string outcome,
        NotificationSeverity severity, IBuildStore builds, ISourceRepositoryStore repositories,
        INotificationDispatcher notify, TimeProvider clock, CancellationToken ct)
    {
        if (IsStale(clock, completedAtUtc)) return;

        var build = await builds.GetByIdAsync(buildId, ct);
        if (build is null) return;

        var name = await RepositoryNameAsync(repositories, build, ct);

        await notify.NotifyAsync(new NotificationMessage(
            $"{icon} Build {outcome} — {name} #{build.CiBuildNumber}",
            severity,
            new[]
            {
                $"Job: {build.CiJobName} #{build.CiBuildNumber}",
                $"Branch: {build.SourceRevision.Branch}",
                $"Commit: {Commit(build)}",
                $"Author: {build.SourceRevision.Author ?? "—"}",
                $"Version: {build.Versions?.PackageVersion ?? "—"}",
                $"Duration: {Duration(build.DurationMs)}",
                $"At: {completedAtUtc:u}",
            },
            // The one click that matters on a failure — straight to the Jenkins console. This is
            // the first use anywhere of NotificationMessage.Link, which both senders already render.
            Link: build.CiRunUrl), ct);
    }

    private static bool IsStale(TimeProvider clock, DateTimeOffset at) =>
        clock.GetUtcNow() - at > MaxAge;

    /// <summary>
    /// The repository NAME lives on a different aggregate — Build holds only RepositoryId. Falls
    /// back to the Jenkins job name, which keeps the title readable if the repo was deleted.
    /// </summary>
    private static async Task<string> RepositoryNameAsync(
        ISourceRepositoryStore repositories, Build build, CancellationToken ct)
    {
        var repo = await repositories.GetByIdAsync(build.RepositoryId, ct);
        return repo?.Name ?? build.CiJobName;
    }

    private static string Commit(Build build)
    {
        var message = build.SourceRevision.Message;
        if (string.IsNullOrWhiteSpace(message)) return build.SourceRevision.CommitShort;

        var trimmed = message.Length > MaxCommitMessageChars
            ? message[..MaxCommitMessageChars] + "…"
            : message;

        return $"{build.SourceRevision.CommitShort} {trimmed}";
    }

    private static string Duration(long? ms) => ms switch
    {
        null or <= 0 => "—",
        < 1000 => $"{ms}ms",
        < 60_000 => $"{ms / 1000.0:0.#}s",
        _ => $"{ms / 60_000}m {ms % 60_000 / 1000}s",
    };
}
