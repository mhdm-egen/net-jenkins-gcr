namespace Cicd.Web.Admin.Services.Sca;

/// <summary>
/// Diffs two parsed SBOMs — "what changed in my dependencies between these two builds".
///
/// Pure and side-effect free, in the same spirit as <see cref="LicenseAnalyzer"/>: given two
/// <see cref="SbomDocument"/>s it always produces the same result, so it is safe to memoize and
/// safe to run on a background thread.
/// </summary>
public static class SbomDiffer
{
    public static SbomDiff Diff(SbomDocument from, SbomDocument to)
    {
        ArgumentNullException.ThrowIfNull(from);
        ArgumentNullException.ThrowIfNull(to);

        var componentChanges = DiffComponents(from, to);
        var vulnChanges      = DiffVulnerabilities(from, to);

        // A vulnerability delta is only meaningful when BOTH documents actually carry a
        // vulnerabilities section. Comparing a Trivy-enriched bom-vex.json against a bare bom.json
        // would otherwise read as "every CVE was fixed", which is the opposite of the truth.
        var vulnComparable = from.HasVulnerabilitiesSection && to.HasVulnerabilitiesSection;

        return new SbomDiff(
            ComponentChanges:      componentChanges,
            VulnerabilityChanges:  vulnComparable ? vulnChanges : [],
            VulnerabilitiesComparable: vulnComparable,
            FromComponentCount:    from.Components.Count,
            ToComponentCount:      to.Components.Count,
            Stats:                 Summarize(componentChanges, vulnComparable ? vulnChanges : []));
    }

    // --- components ---------------------------------------------------------------------------

    private static IReadOnlyList<ComponentChange> DiffComponents(SbomDocument from, SbomDocument to)
    {
        var fromByKey = IndexComponents(from);
        var toByKey   = IndexComponents(to);

        var changes = new List<ComponentChange>();

        foreach (var (key, before) in fromByKey)
        {
            if (toByKey.TryGetValue(key, out var after))
            {
                var kind = ClassifyVersionChange(before.Version, after.Version);
                var licensesChanged = !SameLicenses(before.Licenses, after.Licenses);

                // Unchanged components are deliberately not emitted — on a typical .NET SBOM they
                // are the overwhelming majority and carry no information about what changed.
                if (kind is null && !licensesChanged) continue;

                changes.Add(new ComponentChange(
                    Name:         after.Name ?? before.Name ?? key,
                    Purl:         after.Purl ?? before.Purl,
                    FromVersion:  before.Version,
                    ToVersion:    after.Version,
                    Kind:         kind ?? ComponentChangeKind.LicenseChanged,
                    FromLicenses: before.Licenses,
                    ToLicenses:   after.Licenses));
            }
            else
            {
                changes.Add(new ComponentChange(
                    Name:         before.Name ?? key,
                    Purl:         before.Purl,
                    FromVersion:  before.Version,
                    ToVersion:    null,
                    Kind:         ComponentChangeKind.Removed,
                    FromLicenses: before.Licenses,
                    ToLicenses:   []));
            }
        }

        foreach (var (key, after) in toByKey)
        {
            if (fromByKey.ContainsKey(key)) continue;

            changes.Add(new ComponentChange(
                Name:         after.Name ?? key,
                Purl:         after.Purl,
                FromVersion:  null,
                ToVersion:    after.Version,
                Kind:         ComponentChangeKind.Added,
                FromLicenses: [],
                ToLicenses:   after.Licenses));
        }

        // Most-interesting first: additions and removals change the attack surface, version moves
        // are routine, and a bare licence change is rare but worth surfacing above the noise.
        return changes
            .OrderBy(c => c.Kind switch
            {
                ComponentChangeKind.Added          => 0,
                ComponentChangeKind.Removed        => 1,
                ComponentChangeKind.LicenseChanged => 2,
                ComponentChangeKind.Downgraded     => 3,
                ComponentChangeKind.Upgraded       => 4,
                _                                  => 5,
            })
            .ThenBy(c => c.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>
    /// Keys a component by identity-without-version. A purl is the reliable identity when present
    /// (it disambiguates same-named packages from different ecosystems); name is the fallback.
    /// </summary>
    private static Dictionary<string, SbomComponentEntry> IndexComponents(SbomDocument doc)
    {
        var map = new Dictionary<string, SbomComponentEntry>(StringComparer.OrdinalIgnoreCase);

        foreach (var c in doc.Components)
        {
            var key = ComponentKey(c);
            if (key is null) continue;

            // A malformed SBOM can list the same component twice. First wins; the alternative is
            // dropping one silently at the diff stage, which is harder to notice.
            map.TryAdd(key, c);
        }

        return map;
    }

    private static string? ComponentKey(SbomComponentEntry c)
    {
        if (!string.IsNullOrWhiteSpace(c.Purl))
        {
            // pkg:nuget/Serilog@3.1.1?foo=bar  ->  pkg:nuget/Serilog
            var purl = c.Purl;
            var at = purl.LastIndexOf('@');
            if (at > 0) return purl[..at];

            var q = purl.IndexOf('?');
            return q > 0 ? purl[..q] : purl;
        }

        return string.IsNullOrWhiteSpace(c.Name) ? null : c.Name;
    }

    /// <summary>
    /// Returns null when the versions are equal, otherwise how they moved. Versions that don't
    /// parse as a numeric release are reported as <see cref="ComponentChangeKind.Changed"/> rather
    /// than guessed at — SemVer pre-release ordering is not something to infer from a string.
    /// </summary>
    private static ComponentChangeKind? ClassifyVersionChange(string? before, string? after)
    {
        if (string.Equals(before, after, StringComparison.OrdinalIgnoreCase)) return null;

        if (TryParseRelease(before, out var v1) && TryParseRelease(after, out var v2))
        {
            var cmp = v1.CompareTo(v2);
            if (cmp < 0) return ComponentChangeKind.Upgraded;
            if (cmp > 0) return ComponentChangeKind.Downgraded;

            // Same numeric release, different string — a pre-release suffix moved
            // (1.0.0-beta.1 -> 1.0.0). Real, but not a direction we can assert.
        }

        return ComponentChangeKind.Changed;
    }

    private static bool TryParseRelease(string? version, out Version parsed)
    {
        parsed = new Version(0, 0);
        if (string.IsNullOrWhiteSpace(version)) return false;

        // Strip a SemVer pre-release / build suffix: 1.2.3-preview.4+abc -> 1.2.3
        var span = version.AsSpan();
        var cut  = span.IndexOfAny('-', '+');
        if (cut >= 0) span = span[..cut];

        return Version.TryParse(span, out parsed!);
    }

    private static bool SameLicenses(IReadOnlyList<string> a, IReadOnlyList<string> b) =>
        a.Count == b.Count &&
        a.OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
         .SequenceEqual(b.OrderBy(x => x, StringComparer.OrdinalIgnoreCase), StringComparer.OrdinalIgnoreCase);

    // --- vulnerabilities ----------------------------------------------------------------------

    private static IReadOnlyList<VulnerabilityChange> DiffVulnerabilities(SbomDocument from, SbomDocument to)
    {
        var fromById = IndexVulnerabilities(from);
        var toById   = IndexVulnerabilities(to);

        var changes = new List<VulnerabilityChange>();

        foreach (var (id, v) in toById)
        {
            if (!fromById.ContainsKey(id))
            {
                changes.Add(new VulnerabilityChange(id, Normalize(v.Severity), Introduced: true));
            }
        }

        foreach (var (id, v) in fromById)
        {
            if (!toById.ContainsKey(id))
            {
                changes.Add(new VulnerabilityChange(id, Normalize(v.Severity), Introduced: false));
            }
        }

        // Introduced-before-resolved, worst-first inside each group: the newly critical CVE is the
        // thing a reader needs in the first line, not the low-severity one that went away.
        return changes
            .OrderByDescending(c => c.Introduced)
            .ThenByDescending(c => SeverityRank(c.Severity))
            .ThenBy(c => c.Id, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static Dictionary<string, SbomVulnerability> IndexVulnerabilities(SbomDocument doc)
    {
        var map = new Dictionary<string, SbomVulnerability>(StringComparer.OrdinalIgnoreCase);

        foreach (var v in doc.Vulnerabilities)
        {
            if (string.IsNullOrWhiteSpace(v.Id)) continue;
            map.TryAdd(v.Id, v);
        }

        return map;
    }

    // Matches SbomGraphBuilder's ranking so severity ordering is consistent across the SCA pages.
    private static int SeverityRank(string? s) => s?.ToLowerInvariant() switch
    {
        "critical" => 4,
        "high"     => 3,
        "medium"   => 2,
        "low"      => 1,
        _          => 0,
    };

    private static string Normalize(string? severity) =>
        string.IsNullOrWhiteSpace(severity) ? "unknown" : severity.ToLowerInvariant();

    // --- summary ------------------------------------------------------------------------------

    private static SbomDiffStats Summarize(
        IReadOnlyList<ComponentChange> components,
        IReadOnlyList<VulnerabilityChange> vulns) =>
        new(
            Added:          components.Count(c => c.Kind == ComponentChangeKind.Added),
            Removed:        components.Count(c => c.Kind == ComponentChangeKind.Removed),
            Upgraded:       components.Count(c => c.Kind == ComponentChangeKind.Upgraded),
            Downgraded:     components.Count(c => c.Kind == ComponentChangeKind.Downgraded),
            OtherChanged:   components.Count(c => c.Kind is ComponentChangeKind.Changed or ComponentChangeKind.LicenseChanged),
            VulnsIntroduced: vulns.Count(v => v.Introduced),
            VulnsResolved:   vulns.Count(v => !v.Introduced),
            CriticalOrHighIntroduced: vulns.Count(v => v.Introduced && SeverityRank(v.Severity) >= 3));
}

public sealed record SbomDiff(
    IReadOnlyList<ComponentChange> ComponentChanges,
    IReadOnlyList<VulnerabilityChange> VulnerabilityChanges,
    bool VulnerabilitiesComparable,
    int FromComponentCount,
    int ToComponentCount,
    SbomDiffStats Stats)
{
    /// <summary>True when the two SBOMs describe an identical dependency set.</summary>
    public bool IsEmpty => ComponentChanges.Count == 0 && VulnerabilityChanges.Count == 0;
}

public enum ComponentChangeKind
{
    Added,
    Removed,
    Upgraded,
    Downgraded,
    /// <summary>Version moved, but not in a direction we can assert (pre-release, or unparseable).</summary>
    Changed,
    /// <summary>Same version, different declared licence.</summary>
    LicenseChanged,
}

public sealed record ComponentChange(
    string Name,
    string? Purl,
    string? FromVersion,
    string? ToVersion,
    ComponentChangeKind Kind,
    IReadOnlyList<string> FromLicenses,
    IReadOnlyList<string> ToLicenses)
{
    public bool LicensesChanged =>
        !FromLicenses.OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
                     .SequenceEqual(ToLicenses.OrderBy(x => x, StringComparer.OrdinalIgnoreCase),
                                    StringComparer.OrdinalIgnoreCase);
}

public sealed record VulnerabilityChange(string Id, string Severity, bool Introduced);

public sealed record SbomDiffStats(
    int Added,
    int Removed,
    int Upgraded,
    int Downgraded,
    int OtherChanged,
    int VulnsIntroduced,
    int VulnsResolved,
    int CriticalOrHighIntroduced);
