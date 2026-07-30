using Deployment.Application.Features.AspireApps;
using Deployment.Application.Features.Runs;
using Deployment.Contracts.AspireApps;
using Deployment.Contracts.Metrics;
using Deployment.Contracts.Runs;

namespace Deployment.Application.Features.Metrics;

public sealed record GetDoraSummaryQuery(int Days = 30);

/// <summary>
/// Computes the DORA four server-side over terminal deployment runs — per-service runs and
/// whole-Aspire-app runs together, since both are "a deploy" for delivery-metric purposes.
///
/// Server-side rather than in the UI so the metrics page, the home tile and the weekly digest all
/// report the same numbers from one implementation. (The previous client-side <c>DoraMetrics</c>
/// computed only frequency, change-failure rate and deploy duration.)
///
/// Neither run reader supports a date filter, so this pulls the history and windows it in memory —
/// the same deliberate small-volume choice the metering ledger documents. Push the window into SQL
/// before this gets large.
/// </summary>
public sealed class GetDoraSummaryHandler
{
    private readonly IRunReader _runs;
    private readonly IAspireApplicationRunReader _aspireRuns;
    private readonly TimeProvider _clock;

    public GetDoraSummaryHandler(
        IRunReader runs, IAspireApplicationRunReader aspireRuns, TimeProvider clock)
    {
        _runs = runs;
        _aspireRuns = aspireRuns;
        _clock = clock;
    }

    public async Task<DoraSummaryDto> HandleAsync(GetDoraSummaryQuery q, CancellationToken ct = default)
    {
        var days = Math.Max(1, q.Days);
        var to = _clock.GetUtcNow();
        var from = to.AddDays(-days);

        var points = new List<Point>();

        foreach (var r in await _runs.ListAsync(null, null, ct).ConfigureAwait(false))
            points.Add(new Point(r.ServiceName, r.RequestedAtUtc, r.CompletedAtUtc, r.Status.ToString(), r.CommittedAtUtc));

        // Aspire runs carry no commit provenance, so they contribute to frequency, change-failure
        // rate and MTTR but never to lead time.
        foreach (var a in await _aspireRuns.ListAsync(null, ct).ConfigureAwait(false))
            points.Add(new Point(a.ApplicationName, a.RequestedAtUtc, a.CompletedAtUtc, a.Status.ToString(), null));

        var terminal = points
            .Where(p => p.RequestedAt >= from && p.RequestedAt <= to)
            .Where(p => p.Outcome is Succeeded or Failed or RolledBack)
            .OrderBy(p => p.RequestedAt)
            .ToList();

        var total = terminal.Count;
        var succeeded = terminal.Count(p => p.Outcome == Succeeded);
        var failed = terminal.Count(p => p.Outcome == Failed);
        var rolledBack = terminal.Count(p => p.Outcome == RolledBack);

        var durations = terminal
            .Where(p => p.CompletedAt is not null)
            .Select(p => (p.CompletedAt!.Value - p.RequestedAt).TotalSeconds)
            .Where(s => s >= 0)
            .ToList();

        // Lead time: commit authored -> deploy completed, successful deploys only. A failed deploy
        // never reached production, so including it would not be lead time.
        var leadTimes = terminal
            .Where(p => p.Outcome == Succeeded && p.CommittedAt is not null && p.CompletedAt is not null)
            .Select(p => (p.CompletedAt!.Value - p.CommittedAt!.Value).TotalSeconds)
            .Where(s => s >= 0)
            .ToList();

        var (mttrSeconds, mttrSamples, unrecovered) = ComputeRestoreTimes(terminal);

        return new DoraSummaryDto(
            FromUtc: from,
            ToUtc: to,
            Total: total,
            Succeeded: succeeded,
            Failed: failed,
            RolledBack: rolledBack,
            SuccessRate: total == 0 ? 0 : (double)succeeded / total,
            ChangeFailureRate: total == 0 ? 0 : (double)(failed + rolledBack) / total,
            DeploymentFrequencyPerDay: (double)total / days,
            AvgDeployDurationSeconds: durations.Count == 0 ? 0 : durations.Average(),
            LeadTimeSeconds: leadTimes.Count == 0 ? null : leadTimes.Average(),
            LeadTimeBasis: leadTimes.Count == 0 ? LeadTimeBasisDto.None : LeadTimeBasisDto.CommitToProduction,
            LeadTimeSampleCount: leadTimes.Count,
            MttrSeconds: mttrSeconds,
            MttrSampleCount: mttrSamples,
            UnrecoveredFailures: unrecovered);
    }

    /// <summary>
    /// Mean time to restore, per service: from when a service broke to when it was next deployed
    /// successfully.
    ///
    /// Two judgement calls worth knowing about:
    /// <list type="bullet">
    /// <item>A run of consecutive failures counts ONCE, timed from the FIRST of them — that is when
    /// the service actually broke. Timing from the last failure would flatter the number, and
    /// counting each retry would inflate the sample.</item>
    /// <item>A failure streak still open at the end of the window has no restore time, so it is
    /// excluded from the mean and reported as an unrecovered failure instead. Silently dropping it
    /// would make an ongoing outage improve the metric.</item>
    /// </list>
    /// </summary>
    private static (double? Mttr, int Samples, int Unrecovered) ComputeRestoreTimes(List<Point> terminal)
    {
        var restores = new List<double>();
        var unrecovered = 0;

        foreach (var group in terminal.GroupBy(p => p.ServiceKey, StringComparer.OrdinalIgnoreCase))
        {
            DateTimeOffset? brokeAt = null;

            foreach (var p in group.OrderBy(p => p.RequestedAt))
            {
                var isFailure = p.Outcome is Failed or RolledBack;

                if (isFailure)
                {
                    // Only the first failure of a streak starts the clock.
                    brokeAt ??= p.CompletedAt ?? p.RequestedAt;
                }
                else if (brokeAt is { } start)
                {
                    var restoredAt = p.CompletedAt ?? p.RequestedAt;
                    var seconds = (restoredAt - start).TotalSeconds;
                    if (seconds >= 0) restores.Add(seconds);
                    brokeAt = null;
                }
            }

            if (brokeAt is not null) unrecovered++;
        }

        return (restores.Count == 0 ? null : restores.Average(), restores.Count, unrecovered);
    }

    private const string Succeeded = nameof(DeploymentRunStatusDto.Succeeded);
    private const string Failed = nameof(DeploymentRunStatusDto.Failed);
    private const string RolledBack = nameof(DeploymentRunStatusDto.RolledBack);

    /// <summary>One deploy, normalised across the per-service and Aspire run shapes.</summary>
    private readonly record struct Point(
        string ServiceKey,
        DateTimeOffset RequestedAt,
        DateTimeOffset? CompletedAt,
        string Outcome,
        DateTimeOffset? CommittedAt);
}
