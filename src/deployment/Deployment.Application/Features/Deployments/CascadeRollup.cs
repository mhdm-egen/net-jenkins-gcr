using Deployment.Domain.Deployments;

namespace Deployment.Application.Features.Deployments;

/// <summary>
/// Cascade parent state transitions for v1 (decisions §5.3 — StopAndManual):
///
/// <list type="bullet">
///   <item><c>OnChildFailedAsync</c>: parent immediately flips to Failed.
///     Remaining Queued/Running children are left alone — operators triage.</item>
///   <item><c>OnChildTerminalAsync</c>: parent transitions to Succeeded only
///     once every child has reached a terminal state and all of them succeeded.
///     Lazy-Begin: the parent is Started by the first child to finish, so its
///     StartedAtUtc tracks the cascade's actual start.</item>
/// </list>
///
/// Lives outside any single feature folder because both Succeed and Fail
/// handlers (and the runner) call it.
/// </summary>
internal static class CascadeRollup
{
    public static async Task OnChildTerminalAsync(
        Guid parentId,
        IDeploymentRepository deployments,
        TimeProvider clock,
        CancellationToken cancellationToken)
    {
        var cascade = await deployments.GetCascadeAsync(parentId, cancellationToken).ConfigureAwait(false);
        if (cascade.Count == 0) return;

        var parent = cascade.FirstOrDefault(d => d.Id == parentId);
        if (parent is null) return;

        var children = cascade.Where(d => d.Id != parentId).ToList();
        if (children.Count == 0) return;

        var now = clock.GetUtcNow();
        if (parent.Status == DeploymentStatus.Queued) parent.Start(now);
        if (parent.Status != DeploymentStatus.Running) return;

        var allTerminal = children.All(c => c.IsTerminal);
        if (!allTerminal) return;

        if (children.All(c => c.Status == DeploymentStatus.Succeeded))
        {
            parent.Succeed(now);
        }
        // If not all succeeded but all terminal, OnChildFailedAsync should
        // already have failed the parent at the moment of the first failure.
    }

    public static async Task OnChildFailedAsync(
        Guid parentId,
        string reason,
        IDeploymentRepository deployments,
        TimeProvider clock,
        CancellationToken cancellationToken)
    {
        var cascade = await deployments.GetCascadeAsync(parentId, cancellationToken).ConfigureAwait(false);
        var parent = cascade.FirstOrDefault(d => d.Id == parentId);
        if (parent is null) return;

        var now = clock.GetUtcNow();
        if (parent.Status == DeploymentStatus.Queued) parent.Start(now);

        if (parent.Status == DeploymentStatus.Running)
        {
            parent.Fail(reason, now);
        }
    }
}
