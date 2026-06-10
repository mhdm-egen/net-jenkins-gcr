using Jenkins.Domain.Common;

namespace Jenkins.Domain.Builds;

/// <summary>
/// Supply-chain outputs produced by the build and stored in the Nexus raw repo:
/// the SBOM (<c>bom-vex.json</c>) and the vulnerability report
/// (<c>vulnerabilities.json</c>). These flow straight through to the deployment
/// Release's provenance on handoff.
/// </summary>
public sealed class BuildQuality : ValueObject
{
    public string SbomUri { get; }
    public string VulnerabilityReportUri { get; }

    public BuildQuality(string sbomUri, string vulnerabilityReportUri)
    {
        if (string.IsNullOrWhiteSpace(sbomUri))
            throw new ArgumentException("SbomUri cannot be empty.", nameof(sbomUri));
        if (string.IsNullOrWhiteSpace(vulnerabilityReportUri))
            throw new ArgumentException("VulnerabilityReportUri cannot be empty.", nameof(vulnerabilityReportUri));

        SbomUri = sbomUri.Trim();
        VulnerabilityReportUri = vulnerabilityReportUri.Trim();
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return SbomUri;
        yield return VulnerabilityReportUri;
    }
}
