using Jenkins.Domain.Abstractions;

namespace Jenkins.Domain.Handoffs;

/// <summary>
/// Persistence seam for the <see cref="ContainerReleaseHandoff"/> aggregate.
/// Concrete impl lives in <c>Jenkins.Infrastructure.Persistence.Repositories</c>.
/// </summary>
public interface IContainerReleaseHandoffStore : IRepository<ContainerReleaseHandoff, Guid>
{
    /// <summary>
    /// The most recent handoff for a given build artifact, if any — the idempotency
    /// anchor so re-promoting an already-published artifact returns the prior result.
    /// </summary>
    Task<ContainerReleaseHandoff?> FindLatestByArtifactAsync(Guid buildArtifactId, CancellationToken cancellationToken = default);
}
