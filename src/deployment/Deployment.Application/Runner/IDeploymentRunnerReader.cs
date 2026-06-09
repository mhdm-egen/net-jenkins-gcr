using Deployment.Application.Abstractions;

namespace Deployment.Application.Runner;

/// <summary>
/// Read-model port the deployment runner uses to drive its loop. Lives
/// outside the catalog readers because its queries are scheduler-shaped
/// (next-eligible row, snapshot of execution context) rather than UI-shaped.
/// </summary>
public interface IDeploymentRunnerReader
{
    /// <summary>
    /// Finds the next deployment the runner should pick up. Returns the
    /// oldest leaf row in <c>Queued</c> status (TargetId IS NOT NULL —
    /// cascade parents are lazy-Started by the cascade-rollup helper, not
    /// the runner). Returns null if the queue is empty.
    /// </summary>
    Task<Guid?> FindNextQueuedLeafAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Build the snapshot the adapter needs: release identity, target
    /// descriptor, resolved secret bindings. The caller has already
    /// determined this deployment is ready to run.
    /// </summary>
    Task<DeploymentExecutionContext?> GetExecutionContextAsync(
        Guid deploymentId,
        CancellationToken cancellationToken = default);
}
