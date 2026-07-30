namespace Deployment.Contracts.Metrics;

/// <summary>
/// What a lead-time figure was measured from. On the wire so the UI and the digest can label it
/// instead of implying a DORA-standard number they may not have.
/// </summary>
/// <remarks>
/// There is deliberately no "publish→production" fallback. Nothing on the deploy side records when a
/// specific version was published: the inventory row is keyed by container name, so
/// <c>FirstSeenAtUtc</c> is the first sighting of the name (not this version) and <c>LastSeenAtUtc</c>
/// tracks only the current one. For an auto-triggered run the nearest proxy would be
/// <c>RequestedAtUtc → CompletedAtUtc</c> — which is deploy duration, already reported on its own.
/// A metric with no honest source is worse than an absent one, so lead time reports
/// <see cref="None"/> until commit-carrying deploys exist.
/// </remarks>
public enum LeadTimeBasisDto
{
    /// <summary>No run in the window carried a commit timestamp — lead time is unavailable.</summary>
    None = 0,

    /// <summary>Commit authored → deploy completed. The DORA definition.</summary>
    CommitToProduction = 1,
}

/// <summary>
/// The DORA four plus supporting counts, computed server-side over terminal deployment runs
/// (per-service runs and whole-Aspire-app runs together).
/// </summary>
/// <param name="ChangeFailureRate">(Failed + RolledBack) ÷ total terminal runs. 0–1.</param>
/// <param name="LeadTimeSeconds">
/// Mean commit→production lead time, or null when nothing in the window could be measured. Check
/// <paramref name="LeadTimeBasis"/> before presenting this as "lead time".
/// </param>
/// <param name="MttrSeconds">
/// Mean time to restore: from a Failed/RolledBack run to the next Succeeded run of the SAME service.
/// Null when the window holds no recovered failure — which is not the same as "nothing broke", and
/// <paramref name="UnrecoveredFailures"/> distinguishes the two.
/// </param>
/// <param name="UnrecoveredFailures">
/// Failures with no later success for that service in the window. Excluded from MTTR (no end time
/// yet). Worth surfacing: a high MTTR with none of these is a healthier picture than a low MTTR with
/// several services still down.
/// </param>
public sealed record DoraSummaryDto(
    DateTimeOffset FromUtc,
    DateTimeOffset ToUtc,
    int Total,
    int Succeeded,
    int Failed,
    int RolledBack,
    double SuccessRate,
    double ChangeFailureRate,
    double DeploymentFrequencyPerDay,
    double AvgDeployDurationSeconds,
    double? LeadTimeSeconds,
    LeadTimeBasisDto LeadTimeBasis,
    int LeadTimeSampleCount,
    double? MttrSeconds,
    int MttrSampleCount,
    int UnrecoveredFailures);
