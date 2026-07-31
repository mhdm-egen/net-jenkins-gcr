using System.Globalization;
using System.Text;
using Cicd.Ai;
using Cicd.Notifications;
using Deployment.Contracts.Metrics;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Wolverine;
using Wolverine.Attributes;

namespace Deployment.Application.Features.Metrics;

/// <summary>
/// Configuration for the weekly delivery digest. Bound from <c>Deployment:DoraDigest</c>.
///
/// <see cref="Enabled"/> defaults to FALSE on purpose: this sends email and Slack on a schedule,
/// which is an outward-facing side effect nobody should get by merely upgrading.
/// </summary>
public sealed record DoraDigestOptions
{
    public const string SectionName = "Deployment:DoraDigest";

    public bool Enabled { get; init; }
    public DayOfWeek DayOfWeek { get; init; } = DayOfWeek.Monday;
    public int HourUtc { get; init; } = 8;

    /// <summary>Window the digest reports on. 7 keeps "weekly digest" honest.</summary>
    public int WindowDays { get; init; } = 7;

    /// <summary>
    /// Delay from <paramref name="now"/> until the next configured send slot. Lives here rather than
    /// on the handler because it is purely a function of the schedule settings — and the seeder in
    /// another assembly needs it too.
    ///
    /// Always at least an hour out, so a fire landing inside its own slot cannot schedule a successor
    /// for the same instant and spin.
    /// </summary>
    public TimeSpan UntilNext(DateTimeOffset now)
    {
        var hour = Math.Clamp(HourUtc, 0, 23);
        var slot = new DateTimeOffset(now.UtcDateTime.Date, TimeSpan.Zero).AddHours(hour);

        var daysAhead = ((int)DayOfWeek - (int)slot.DayOfWeek + 7) % 7;
        slot = slot.AddDays(daysAhead);
        if (slot <= now.AddHours(1)) slot = slot.AddDays(7);

        return slot - now;
    }
}

/// <summary>
/// Time to send the delivery digest.
/// </summary>
/// <param name="Force">
/// Set by the manual trigger. Bypasses the already-sent-this-week guard, and deliberately does NOT
/// schedule a successor — otherwise every button press would fork a second recurring chain.
/// </param>
public sealed record WeeklyDoraDigestDue(bool Force = false);

/// <summary>
/// Computes the delivery summary, has the model write a short narrative over it, pushes both through
/// <see cref="INotificationDispatcher"/> (Slack + email, whichever are configured), then schedules
/// the next occurrence — so the recurrence lives in Wolverine's durable scheduling rather than in a
/// process-local timer, and survives restarts.
///
/// Wolverine's scheduling is a one-shot delay, so "weekly" is a self-rescheduling chain. Two
/// properties keep that chain from multiplying: a per-ISO-week marker means a duplicate fire is a
/// no-op, and a run that skips does NOT reschedule. So if restarts seed extra chains, the first fire
/// of the week sends and reschedules while the rest skip and die out — the chain self-prunes back to
/// one.
///
/// Known race: the marker is written after a successful send, not claimed atomically before it
/// (IDistributedCache has no compare-and-set). Two chains firing in the same instant can therefore
/// both send. Send-then-mark is the deliberate choice over mark-then-send: a duplicate digest is
/// recoverable, a silently skipped one is not.
///
/// [WolverineHandler] is REQUIRED — Wolverine only auto-discovers types ending in
/// "Handler"/"Consumer", and this one qualifies, but the attribute documents the intent.
/// </summary>
[WolverineHandler]
public sealed class WeeklyDoraDigestHandler
{
    private const string Feature = "dora_digest";

    private const string SystemPrompt =
        "You are writing a short weekly delivery-performance note for engineering leadership, from " +
        "DORA metrics. Ground every statement in the supplied figures — do NOT invent trends, " +
        "comparisons to previous weeks, service names, or causes that are not in the data. You have " +
        "no history, so do not imply direction ('improving', 'up from last week') unless a figure " +
        "says so. Where a metric is unavailable, say plainly that it is unavailable and why, rather " +
        "than omitting it or substituting another number. Call out sample sizes when they are small " +
        "enough to make an average unreliable. Lead with the single most important thing. Keep it to " +
        "a few short paragraphs, plain prose, no headings, no bullet lists, no markdown.";

    public async Task Handle(
        WeeklyDoraDigestDue evt,
        GetDoraSummaryHandler dora,
        AiExplanationRunner ai,
        INotificationDispatcher notify,
        IDistributedCache cache,
        IOptionsMonitor<DoraDigestOptions> digestOptions,
        IOptionsMonitor<NotificationOptions> notifyOptions,
        IMessageBus bus,
        TimeProvider clock,
        ILogger<WeeklyDoraDigestHandler> logger,
        CancellationToken ct)
    {
        var options = digestOptions.CurrentValue;
        var now = clock.GetUtcNow();
        var weekKey = WeekKey(now);
        var sentMarker = $"dora-digest:sent:{weekKey}";

        if (!evt.Force)
        {
            var already = await cache.GetStringAsync(sentMarker, ct).ConfigureAwait(false);
            if (already is { Length: > 0 })
            {
                // Skip AND do not reschedule: this is how a duplicate chain dies out.
                logger.LogInformation("[digest] {Week} already sent — skipping, and not rescheduling (duplicate chain).", weekKey);
                return;
            }
        }

        var summary = await dora.HandleAsync(new GetDoraSummaryQuery(Math.Max(1, options.WindowDays)), ct)
            .ConfigureAwait(false);

        var narrative = await WriteNarrativeAsync(ai, summary, weekKey, logger, ct).ConfigureAwait(false);

        // OnlyFailures suppresses anything that isn't a Failure, and a digest is Info — so it would
        // vanish with no trace. Detect it here so the log (and the manual trigger's caller) can say so.
        if (notifyOptions.CurrentValue.OnlyFailures)
            logger.LogWarning(
                "[digest] Notifications are set to OnlyFailures, so this digest will be suppressed. " +
                "Clear Deployment:Notifications:OnlyFailures to receive it.");

        await notify.NotifyAsync(
            new NotificationMessage(
                Title: $"Delivery digest — {options.WindowDays} days to {now:yyyy-MM-dd}",
                Severity: NotificationSeverity.Info,
                Fields: BuildFields(summary, narrative)),
            ct).ConfigureAwait(false);

        await cache.SetStringAsync(sentMarker, now.ToString("O"), new DistributedCacheEntryOptions
        {
            // Comfortably longer than the cadence, short enough not to accumulate forever.
            AbsoluteExpirationRelativeToNow = TimeSpan.FromDays(30),
        }, ct).ConfigureAwait(false);

        logger.LogInformation("[digest] Sent {Week} digest ({Total} deploys, CFR {Cfr:P0}).",
            weekKey, summary.Total, summary.ChangeFailureRate);

        // Only the scheduled path continues the chain; a manual press must not fork one.
        if (!evt.Force && options.Enabled)
        {
            var delay = options.UntilNext(now);
            await bus.ScheduleAsync(new WeeklyDoraDigestDue(), delay).ConfigureAwait(false);
            logger.LogInformation("[digest] Next digest scheduled in {Days:0.0} day(s).", delay.TotalDays);
        }
    }

    /// <summary>
    /// The model's narrative, cached per week so a retry doesn't pay twice. Falls back to
    /// numbers-only on any failure — a digest that arrives without prose beats one that never
    /// arrives because the model was unreachable.
    /// </summary>
    private static async Task<string?> WriteNarrativeAsync(
        AiExplanationRunner ai, DoraSummaryDto s, string weekKey, ILogger logger, CancellationToken ct)
    {
        if (!ai.IsConfigured) return null;

        try
        {
            var outcome = await ai.RunAsync(
                cacheKey: $"dora-digest:v1:{weekKey}",
                feature: Feature,
                tier: AiModelKind.Interactive,
                systemPrompt: SystemPrompt,
                groundedPrompt: BuildPrompt(s),
                ttl: TimeSpan.FromDays(30),
                ct: ct).ConfigureAwait(false);
            return string.IsNullOrWhiteSpace(outcome.Text) ? null : outcome.Text;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "[digest] Narrative generation failed — sending figures only.");
            return null;
        }
    }

    private static string BuildPrompt(DoraSummaryDto s)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Window: {s.FromUtc:yyyy-MM-dd} to {s.ToUtc:yyyy-MM-dd}");
        sb.AppendLine($"Deploys (terminal runs): {s.Total} — {s.Succeeded} succeeded, {s.Failed} failed, {s.RolledBack} rolled back");
        sb.AppendLine($"Deployment frequency: {s.DeploymentFrequencyPerDay:0.00} per day");
        sb.AppendLine($"Change-failure rate: {s.ChangeFailureRate:P1}");
        sb.AppendLine($"Success rate: {s.SuccessRate:P1}");
        sb.AppendLine($"Average deploy duration: {s.AvgDeployDurationSeconds:0.0}s (requested → settled; this is NOT lead time)");
        sb.AppendLine();

        sb.AppendLine(s.LeadTimeSeconds is { } lt
            ? $"Lead time (commit authored → deployed): {lt:0.0}s, averaged over {s.LeadTimeSampleCount} deploy(s)."
            : "Lead time: UNAVAILABLE. No deploy in this window carried a commit timestamp, so " +
              "commit→production cannot be measured. There is no substitute metric — do not offer one.");

        sb.AppendLine(s.MttrSeconds is { } mttr
            ? $"Time to restore: {mttr:0.0}s, averaged over {s.MttrSampleCount} recovered failure(s)."
            : "Time to restore: UNAVAILABLE — no failure in this window was followed by a successful " +
              "deploy of the same service.");

        sb.AppendLine(s.UnrecoveredFailures > 0
            ? $"Unrecovered failures: {s.UnrecoveredFailures} service(s) failed and have had no " +
              "successful deploy since. These are excluded from time-to-restore because they have " +
              "not been restored — mention this, it matters more than the averages."
            : "Unrecovered failures: none — every failure in the window was followed by a success.");

        return sb.ToString();
    }

    private static IReadOnlyList<string> BuildFields(DoraSummaryDto s, string? narrative)
    {
        var fields = new List<string>();
        if (!string.IsNullOrWhiteSpace(narrative)) fields.Add(narrative!);

        fields.Add($"Deploys: {s.Total} ({s.Succeeded} ok / {s.Failed} failed / {s.RolledBack} rolled back)");
        fields.Add($"Frequency: {s.DeploymentFrequencyPerDay:0.00} per day");
        fields.Add($"Change-failure rate: {s.ChangeFailureRate:P0}");
        fields.Add($"Avg deploy duration: {s.AvgDeployDurationSeconds:0.0}s");
        fields.Add(s.LeadTimeSeconds is { } lt
            ? $"Lead time: {FormatDuration(lt)} (n={s.LeadTimeSampleCount})"
            : "Lead time: unavailable — no commit-dated deploys in window");
        fields.Add(s.MttrSeconds is { } mttr
            ? $"Time to restore: {FormatDuration(mttr)} (n={s.MttrSampleCount})"
            : "Time to restore: unavailable — no recovered failures in window");
        if (s.UnrecoveredFailures > 0)
            fields.Add($"⚠ {s.UnrecoveredFailures} service(s) still failing (excluded from time-to-restore)");

        return fields;
    }

    private static string FormatDuration(double seconds) => seconds switch
    {
        >= 86400 => $"{seconds / 86400:0.0}d",
        >= 3600 => $"{seconds / 3600:0.0}h",
        >= 60 => $"{seconds / 60:0.0}m",
        _ => $"{seconds:0.0}s",
    };

    /// <summary>ISO-8601 year-week, e.g. <c>2026-W31</c> — the idempotency key for "this week".</summary>
    internal static string WeekKey(DateTimeOffset at)
    {
        var d = at.UtcDateTime;
        return $"{ISOWeek.GetYear(d):0000}-W{ISOWeek.GetWeekOfYear(d):00}";
    }
}
