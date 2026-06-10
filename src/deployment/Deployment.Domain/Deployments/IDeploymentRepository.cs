using Deployment.Domain.Abstractions;

namespace Deployment.Domain.Deployments;

public interface IDeploymentRepository : IRepository<Deployment, Guid>
{
    /// <summary>
    /// The most recent <see cref="DeploymentStatus.Succeeded"/> deployment of
    /// any release of the given service in the given environment. Backs the
    /// <c>Current</c> pin resolution (decisions §3). Returns null if the service
    /// has never been successfully deployed in this environment.
    /// </summary>
    Task<Deployment?> FindLatestSucceededAsync(
        Guid deployableUnitId,
        Guid environmentId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Load the parent + all child rows of a cascade in a single round-trip.
    /// Returns just the matching row (and a null list) for non-cascade deploys.
    /// </summary>
    Task<IReadOnlyList<Deployment>> GetCascadeAsync(
        Guid parentDeploymentId,
        CancellationToken cancellationToken = default);
}
