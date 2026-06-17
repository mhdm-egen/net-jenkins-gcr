using Jenkins.Application.Features.Builds;

namespace Jenkins.Infrastructure.Sync.Nexus;

/// <summary>
/// Resolves the artifacts a build produced from Nexus (the system of record),
/// matched by the build's package version (NuGet) and commit-short / <c>ci-N</c> tag
/// (Docker). Returns reconciliation specs the build-sync attaches to the build.
/// Registered only when Nexus is configured.
/// </summary>
public interface INexusArtifactReader
{
    Task<IReadOnlyList<ArtifactSpec>> FindArtifactsAsync(
        string packageVersion,
        string commitShort,
        int ciBuildNumber,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// The durable Nexus base URL for a build's SBOM artifacts, keyed by package version
    /// (the cicd-scan job uploads <c>bom-vex.json</c> + <c>vulnerabilities.json</c> there).
    /// e.g. <c>http://nexus:8081/repository/sboms/sbom/{version}</c>.
    /// </summary>
    string SbomBaseUrl(string packageVersion);
}
