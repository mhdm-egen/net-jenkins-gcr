using Deployment.Contracts.Deployments;
using Deployment.Contracts.Environments;
using Deployment.Contracts.Releases;

namespace Deployment.Application.Abstractions;

/// <summary>
/// Per-<see cref="TargetKindDto"/> driver that does the actual deployment work
/// — calling the target's Admin/Management API (App Service, Cloud Run, K8s,
/// IIS, …) and reporting back the outcome. The runner picks one adapter per
/// row based on the row's target kind via <see cref="IDeploymentAdapterRegistry"/>.
///
/// Adapters are deliberately decoupled from the domain: they receive a
/// snapshot of everything they need (release identity, target descriptor,
/// resolved secret bindings) and return success/failure. They never touch
/// the DB or domain types — that's the runner's job after this returns.
/// </summary>
public interface IDeploymentAdapter
{
    /// <summary>The <see cref="TargetKindDto"/> this adapter handles.</summary>
    TargetKindDto TargetKind { get; }

    /// <summary>
    /// Execute the deployment. Implementations may take seconds (slot swap)
    /// or minutes (canary observation). Honor <paramref name="cancellationToken"/>
    /// — the runner uses it to shut down gracefully.
    /// </summary>
    Task<DeploymentExecutionOutcome> ExecuteAsync(
        DeploymentExecutionContext context,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// What the adapter is being asked to do. Snapshot of the row, the release
/// artifact, the target, and the resolved secret bindings (no live DB access
/// from the adapter).
///
/// <see cref="ArtifactType"/>/<see cref="ArtifactUri"/> carry the thing to
/// deploy. For container targets (Cloud Run, Container Apps, K8s) the adapter
/// reads <see cref="ArtifactUri"/> as the image reference — deploy by digest,
/// e.g. <c>{region}-docker.pkg.dev/{project}/{repo}/{svc}@sha256:…</c>.
/// </summary>
public sealed record DeploymentExecutionContext(
    Guid DeploymentId,
    Guid ReleaseId,
    string ReleaseSemanticVersion,
    ArtifactTypeDto ArtifactType,
    string? ArtifactUri,
    Guid DeployableUnitId,
    string DeployableUnitName,
    DeploymentTargetDescriptor Target,
    DeploymentStrategyDto Strategy,
    IReadOnlyList<ResolvedSecretBinding> SecretBindings);

public sealed record DeploymentTargetDescriptor(
    Guid Id,
    TargetKindDto TargetKind,
    string ResourceId,
    string Region,
    string? Slot);

public sealed record ResolvedSecretBinding(
    Guid ConfigurationSettingId,
    string Key,
    string VersionedSecretUri);

/// <summary>
/// The result of an adapter run. Adapters may also record finer-grained
/// audit events during the run via <c>IDeploymentRunnerHost.RecordAuditAsync</c>;
/// this is the terminal verdict.
/// </summary>
public sealed record DeploymentExecutionOutcome(
    bool IsSuccess,
    string? FailureReason)
{
    public static DeploymentExecutionOutcome Success { get; } = new(true, null);
    public static DeploymentExecutionOutcome Failure(string reason) => new(false, reason);
}

/// <summary>
/// Looks up the right <see cref="IDeploymentAdapter"/> for a given target kind.
/// The default implementation is just a dictionary populated at startup; one
/// adapter per kind. Resolution failures are explicit — the runner fails the
/// deployment with "no adapter registered" rather than silently no-op.
/// </summary>
public interface IDeploymentAdapterRegistry
{
    IDeploymentAdapter Resolve(TargetKindDto kind);
    bool TryResolve(TargetKindDto kind, out IDeploymentAdapter? adapter);
}
