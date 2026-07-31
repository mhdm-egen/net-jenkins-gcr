namespace Cicd.Web.Admin.Services.Deployment;

/// <summary>How prominently an advisory should be shown.</summary>
public enum AdvisoryLevel
{
    /// <summary>Transient / informational — e.g. the ingress controller has not claimed the Ingress yet.</summary>
    Note,

    /// <summary>Something needs acting on — e.g. no ingress controller, so the URL cannot serve.</summary>
    Warning,
}

/// <summary>One advisory line lifted out of a deploy log.</summary>
public sealed record DeploymentAdvisory(AdvisoryLevel Level, string Message);

/// <summary>
/// Pulls advisory lines out of a deploy/preview log so the UI can show them as alerts instead of
/// burying them in the log pane.
///
/// The deployment service writes these as plain "WARNING: …" / "NOTE: …" lines on the run log (see
/// IIngressManager.DescribeUnbackedIngressAsync). They matter because the run can SUCCEED while the
/// URL it hands back is unreachable — a missing ingress controller looks like a broken app unless the
/// reason is visible next to the link.
///
/// Presentation-only, and deliberately forgiving: an unrecognised log is simply shown as-is, so a
/// change to the wording downgrades the alert to plain log text rather than breaking the page.
/// </summary>
public static class DeploymentAdvisories
{
    private const string WarningPrefix = "WARNING:";
    private const string NotePrefix = "NOTE:";

    public static IReadOnlyList<DeploymentAdvisory> Extract(string? log)
    {
        if (string.IsNullOrWhiteSpace(log)) return [];

        var found = new List<DeploymentAdvisory>();
        foreach (var raw in log.Split('\n'))
        {
            var line = raw.Trim();
            if (line.StartsWith(WarningPrefix, StringComparison.OrdinalIgnoreCase))
                found.Add(new DeploymentAdvisory(AdvisoryLevel.Warning, line[WarningPrefix.Length..].Trim()));
            else if (line.StartsWith(NotePrefix, StringComparison.OrdinalIgnoreCase))
                found.Add(new DeploymentAdvisory(AdvisoryLevel.Note, line[NotePrefix.Length..].Trim()));
        }

        return found;
    }

    /// <summary>True when the log carries an advisory that should discourage trusting the app URL.</summary>
    public static bool HasWarning(string? log) =>
        Extract(log).Any(a => a.Level == AdvisoryLevel.Warning);
}
