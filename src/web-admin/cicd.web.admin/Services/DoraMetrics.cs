namespace Cicd.Web.Admin.Services;

/// <summary>DORA-style delivery summary over terminal deploy runs.</summary>
public sealed record DoraSummary(
    int Total, int Succeeded, int Failed, int RolledBack,
    double SuccessRate, double ChangeFailureRate, double AvgDurationSeconds, double FrequencyPerDay);

/// <summary>
/// Computes the delivery summary from deploy points — shared by the Deployment → Metrics page and the Home
/// dashboard's DORA tile so both report identical figures. A "deploy point" is (requested, completed?, outcome),
/// where outcome is the run's status string (<c>Succeeded</c>/<c>Failed</c>/<c>RolledBack</c>/…).
/// </summary>
public static class DoraMetrics
{
    public static DoraSummary Compute(IEnumerable<(DateTimeOffset RequestedAt, DateTimeOffset? CompletedAt, string Outcome)> points)
    {
        var terminal = points.Where(p => p.Outcome is "Succeeded" or "Failed" or "RolledBack").ToList();
        var total = terminal.Count;
        var succeeded = terminal.Count(p => p.Outcome == "Succeeded");
        var failed = terminal.Count(p => p.Outcome == "Failed");
        var rolledBack = terminal.Count(p => p.Outcome == "RolledBack");
        var successRate = total == 0 ? 0 : (double)succeeded / total;
        var cfr = total == 0 ? 0 : (double)(failed + rolledBack) / total;

        var durations = terminal
            .Where(p => p.CompletedAt is not null)
            .Select(p => (p.CompletedAt!.Value - p.RequestedAt).TotalSeconds)
            .Where(s => s >= 0)
            .ToList();
        var avg = durations.Count == 0 ? 0 : durations.Average();

        var today = DateTimeOffset.Now.Date;
        var last7 = terminal.Count(p => p.RequestedAt.LocalDateTime.Date >= today.AddDays(-6));

        return new DoraSummary(total, succeeded, failed, rolledBack, successRate, cfr, avg, last7 / 7.0);
    }
}
