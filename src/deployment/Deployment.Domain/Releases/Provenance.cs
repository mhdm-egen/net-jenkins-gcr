using Deployment.Domain.Common;

namespace Deployment.Domain.Releases;

/// <summary>
/// Supply-chain provenance attached to a release at publish time
/// (decisions §9.1). All six fields are populated together by the publish
/// pipeline; releases predating provenance support carry a null instance.
/// </summary>
public sealed class Provenance : ValueObject
{
    /// <summary>Hex SHA-256 of the artifact at publish time (integrity check).</summary>
    public string ArtifactSha256 { get; }

    /// <summary>URI to the SBOM (e.g., <c>bom-vex.json</c> in Nexus raw).</summary>
    public string SbomUri { get; }

    /// <summary>URI to the vulnerability report (e.g., Trivy <c>vulnerabilities.json</c>).</summary>
    public string VulnerabilityReportUri { get; }

    /// <summary>Human-clickable CI run URL.</summary>
    public string CiRunUrl { get; }

    /// <summary>Programmatic CI run identifier (e.g., <c>cicd-build/#42</c>).</summary>
    public string CiRunId { get; }

    /// <summary>Identity that published the release (often a system account).</summary>
    public string PublishedByPrincipal { get; }

    public Provenance(
        string artifactSha256,
        string sbomUri,
        string vulnerabilityReportUri,
        string ciRunUrl,
        string ciRunId,
        string publishedByPrincipal)
    {
        if (string.IsNullOrWhiteSpace(artifactSha256))
            throw new ArgumentException("ArtifactSha256 cannot be empty.", nameof(artifactSha256));
        if (string.IsNullOrWhiteSpace(sbomUri))
            throw new ArgumentException("SbomUri cannot be empty.", nameof(sbomUri));
        if (string.IsNullOrWhiteSpace(vulnerabilityReportUri))
            throw new ArgumentException("VulnerabilityReportUri cannot be empty.", nameof(vulnerabilityReportUri));
        if (string.IsNullOrWhiteSpace(ciRunUrl))
            throw new ArgumentException("CiRunUrl cannot be empty.", nameof(ciRunUrl));
        if (string.IsNullOrWhiteSpace(ciRunId))
            throw new ArgumentException("CiRunId cannot be empty.", nameof(ciRunId));
        if (string.IsNullOrWhiteSpace(publishedByPrincipal))
            throw new ArgumentException("PublishedByPrincipal cannot be empty.", nameof(publishedByPrincipal));

        ArtifactSha256 = artifactSha256.Trim();
        SbomUri = sbomUri.Trim();
        VulnerabilityReportUri = vulnerabilityReportUri.Trim();
        CiRunUrl = ciRunUrl.Trim();
        CiRunId = ciRunId.Trim();
        PublishedByPrincipal = publishedByPrincipal.Trim();
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return ArtifactSha256;
        yield return SbomUri;
        yield return VulnerabilityReportUri;
        yield return CiRunUrl;
        yield return CiRunId;
        yield return PublishedByPrincipal;
    }
}
