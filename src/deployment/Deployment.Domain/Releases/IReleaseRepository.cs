using Deployment.Domain.Abstractions;

namespace Deployment.Domain.Releases;

/// <summary>
/// Persistence seam for the <see cref="Release"/> aggregate. The query helpers
/// here back the pin-resolution ladder (decisions §3) — keeping them on the
/// repository keeps the resolver pure of EF dependencies.
/// </summary>
public interface IReleaseRepository : IRepository<Release, Guid>
{
    /// <summary>
    /// Newest <see cref="ReleaseStatus.Available"/> release for the given unit,
    /// or null if none exist. Used by <c>Latest</c> pin resolution.
    /// </summary>
    Task<Release?> FindLatestAvailableAsync(
        Guid deployableUnitId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Find a release by its catalog-unique (DeployableUnitId, SemanticVersion) pair.
    /// </summary>
    Task<Release?> FindByVersionAsync(
        Guid deployableUnitId,
        string semanticVersion,
        CancellationToken cancellationToken = default);
}
