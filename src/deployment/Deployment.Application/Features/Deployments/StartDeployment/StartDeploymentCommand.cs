using Deployment.Contracts.Deployments;

namespace Deployment.Application.Features.Deployments.StartDeployment;

/// <summary>
/// Kick off a deployment of <paramref name="ReleaseId"/> into
/// <paramref name="EnvironmentId"/>. The handler enforces:
/// promotion-path soft-policing (§7.2), freeze-window soft-policing (§7.4),
/// pin resolution (§3), config + secret-binding snapshot (§4), and the
/// app-cascade fan-out convention (§10.2). Status starts at <c>Queued</c>;
/// the deployment runner picks rows up from there.
///
/// <paramref name="TargetIds"/>: leave empty to deploy to every
/// <c>DeploymentTarget</c> in the environment, or specify a subset.
/// </summary>
public sealed record StartDeploymentCommand(
    Guid ReleaseId,
    Guid EnvironmentId,
    IReadOnlyList<Guid> TargetIds,
    DeploymentStrategyDto Strategy,
    DeploymentTriggerDto Trigger,
    string TriggeredByPrincipal,
    string? SkipPromotionPathReason,
    string? OverrideFreezeReason);

public sealed record StartedDeployment(
    Guid? ParentDeploymentId,
    IReadOnlyList<Guid> ChildDeploymentIds);
