using System.Text;

namespace Cicd.Web.Admin.Services.Sca;

/// <summary>
/// The grounded input for "what changed in my dependencies between these two builds".
/// Assembled entirely from <see cref="SbomDiffer"/> output — no free text reaches the model that
/// the platform didn't compute.
/// </summary>
public sealed record SbomDiffExplainRequest(
    string JobName,
    int FromBuild,
    int ToBuild,
    SbomDiffStats Stats,
    int FromComponentCount,
    int ToComponentCount,
    bool VulnerabilitiesComparable,
    IReadOnlyList<ComponentChange> ComponentChanges,
    IReadOnlyList<VulnerabilityChange> VulnerabilityChanges,
    int OmittedComponentChanges)
{
    /// <summary>
    /// A dependency diff can run to hundreds of rows on a framework bump. The ordering out of
    /// <see cref="SbomDiffer"/> is already most-interesting-first, so a head-cut keeps the rows
    /// that matter; the count of what was dropped goes into the prompt so the model says
    /// "and N more" instead of implying it saw everything.
    /// </summary>
    public const int MaxComponentRows = 60;

    public static SbomDiffExplainRequest FromDiff(string jobName, int fromBuild, int toBuild, SbomDiff diff)
    {
        var shown   = diff.ComponentChanges.Take(MaxComponentRows).ToList();
        var omitted = diff.ComponentChanges.Count - shown.Count;

        return new SbomDiffExplainRequest(
            JobName:                   jobName,
            FromBuild:                 fromBuild,
            ToBuild:                   toBuild,
            Stats:                     diff.Stats,
            FromComponentCount:        diff.FromComponentCount,
            ToComponentCount:          diff.ToComponentCount,
            VulnerabilitiesComparable: diff.VulnerabilitiesComparable,
            ComponentChanges:          shown,
            VulnerabilityChanges:      diff.VulnerabilityChanges,
            OmittedComponentChanges:   omitted);
    }

    public string ToPromptBlock()
    {
        var sb = new StringBuilder();

        sb.AppendLine($"Jenkins job: {JobName}");
        sb.AppendLine($"Comparing build #{FromBuild} (before) to build #{ToBuild} (after).");
        sb.AppendLine($"Component count: {FromComponentCount} -> {ToComponentCount}.");
        sb.AppendLine();

        sb.AppendLine("Summary of changes:");
        sb.AppendLine($"- added: {Stats.Added}");
        sb.AppendLine($"- removed: {Stats.Removed}");
        sb.AppendLine($"- upgraded: {Stats.Upgraded}");
        sb.AppendLine($"- downgraded: {Stats.Downgraded}");
        sb.AppendLine($"- other version/licence changes: {Stats.OtherChanged}");
        sb.AppendLine();

        if (VulnerabilitiesComparable)
        {
            sb.AppendLine($"Vulnerabilities introduced: {Stats.VulnsIntroduced} " +
                          $"(of which critical or high: {Stats.CriticalOrHighIntroduced})");
            sb.AppendLine($"Vulnerabilities no longer present: {Stats.VulnsResolved}");

            if (VulnerabilityChanges.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("Vulnerability changes:");
                foreach (var v in VulnerabilityChanges)
                {
                    sb.AppendLine($"- {(v.Introduced ? "INTRODUCED" : "resolved")} {v.Id} (severity: {v.Severity})");
                }
            }
        }
        else
        {
            sb.AppendLine("Vulnerability comparison is UNAVAILABLE: at least one of the two SBOMs has no " +
                          "vulnerabilities section, so no statement can be made about CVEs either way.");
        }

        sb.AppendLine();
        sb.AppendLine("Component changes:");

        if (ComponentChanges.Count == 0)
        {
            sb.AppendLine("(none)");
        }
        else
        {
            foreach (var c in ComponentChanges)
            {
                sb.Append("- ").Append(c.Kind.ToString().ToUpperInvariant()).Append(' ').Append(c.Name);

                sb.Append(c.Kind switch
                {
                    ComponentChangeKind.Added   => $" {c.ToVersion}",
                    ComponentChangeKind.Removed => $" {c.FromVersion}",
                    _                           => $" {c.FromVersion} -> {c.ToVersion}",
                });

                if (c.LicensesChanged)
                {
                    sb.Append($" [licence: {Join(c.FromLicenses)} -> {Join(c.ToLicenses)}]");
                }

                sb.AppendLine();
            }

            if (OmittedComponentChanges > 0)
            {
                sb.AppendLine($"... and {OmittedComponentChanges} further component changes not listed here.");
            }
        }

        return sb.ToString();
    }

    private static string Join(IReadOnlyList<string> licenses) =>
        licenses.Count == 0 ? "undeclared" : string.Join(", ", licenses);
}
