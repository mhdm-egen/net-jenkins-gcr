namespace Deployment.Application.Abstractions;

/// <summary>
/// Ensures a container image is present in a deployment-target-reachable registry
/// — for GCP targets, Google Artifact Registry — copied digest-preserving from the
/// canonical source registry (Nexus).
///
/// Decision #6: registry promotion is a deployment concern, not CI. The Release's
/// <c>ArtifactUri</c> carries the Nexus digest ref; the GCP target adapters resolve
/// it to a GAR ref via this port immediately before deploying.
/// </summary>
public interface IArtifactPromoter
{
    /// <summary>
    /// Copy <paramref name="sourceRef"/> to <paramref name="destinationRef"/> if it
    /// isn't already there. Both are pull-by-digest references; the copy preserves
    /// the digest. Idempotent — re-copying an already-present digest is a no-op.
    /// </summary>
    Task EnsureCopiedAsync(string sourceRef, string destinationRef, CancellationToken cancellationToken = default);
}
