namespace Jenkins.Application.Abstractions;

/// <summary>
/// The one-way handoff port to the deployment microservice (handoff §7). The
/// Application layer speaks these CI-local inputs; the Infrastructure adapter maps
/// them to <c>Deployment.Contracts</c> and calls <c>POST /api/deployment/releases</c>
/// then <c>POST /releases/{id}/provenance</c>. Keeping the port CI-local means the
/// Application layer carries no dependency on the other service's wire types.
/// </summary>
public interface IDeploymentReleaseClient
{
    /// <summary>
    /// Publish a container Release in the deployment service. Returns the new
    /// Release id. Implementations treat a duplicate-version conflict (409) as
    /// already-published and return the existing id (CI decision #4 idempotency).
    /// </summary>
    Task<Guid> PublishContainerReleaseAsync(PublishReleaseInput input, CancellationToken ct = default);

    /// <summary>Attach supply-chain provenance to a just-published Release.</summary>
    Task AttachProvenanceAsync(Guid releaseId, AttachProvenanceInput input, CancellationToken ct = default);
}

/// <summary>
/// What CI sends to create a container Release. <c>ArtifactType</c> is implicitly
/// ContainerImage; <c>ArtifactUri</c> is the Nexus digest ref (decision #6).
/// </summary>
public sealed record PublishReleaseInput(
    Guid DeployableUnitId,
    string SemanticVersion,
    string BuildNumber,
    string CommitSha,
    string ArtifactUri);

/// <summary>The six provenance fields, sourced from the build (handoff §7 mapping).</summary>
public sealed record AttachProvenanceInput(
    string ArtifactSha256,
    string SbomUri,
    string VulnerabilityReportUri,
    string CiRunUrl,
    string CiRunId,
    string PublishedByPrincipal);
