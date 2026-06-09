using Deployment.Domain.Deployments;

namespace Deployment.Application.Features.Deployments.GetDeploymentBaseline;

/// <summary>
/// Q2 — full snapshot of what a single deployment installed: the release
/// metadata, all per-leaf rows (for a cascade parent), and each leaf's
/// secret bindings. Used for incident-response "what was deployed at time T?".
/// </summary>
public sealed record GetDeploymentBaselineQuery(Guid DeploymentId);

public sealed record DeploymentBaseline(
    Guid DeploymentId,
    Guid ReleaseId,
    string ReleaseSemanticVersion,
    Guid EnvironmentId,
    DeploymentStatus Status,
    DeploymentStrategy Strategy,
    DeploymentTrigger Trigger,
    string TriggeredByPrincipal,
    DateTimeOffset? StartedAtUtc,
    DateTimeOffset? CompletedAtUtc,
    string? SkipPromotionPathReason,
    string? OverrideFreezeReason,
    IReadOnlyList<BaselineLeaf> Leaves);

public sealed record BaselineLeaf(
    Guid DeploymentId,
    Guid ReleaseId,
    string ReleaseSemanticVersion,
    Guid? TargetId,
    string? TargetResourceId,
    DeploymentStatus Status,
    IReadOnlyList<BaselineSecretBinding> SecretBindings);

public sealed record BaselineSecretBinding(
    Guid ConfigurationSettingId,
    string ConfigurationKey,
    string ResolvedSecretUri);
