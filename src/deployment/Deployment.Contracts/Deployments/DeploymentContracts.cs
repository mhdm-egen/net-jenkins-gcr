namespace Deployment.Contracts.Deployments;

// Wire-stable mirrors of the Deployment-side enums. Integer values must match
// Deployment.Domain.Deployments enums one-for-one.

public enum DeploymentStatusDto
{
    Queued = 0,
    Running = 1,
    Succeeded = 2,
    Failed = 3,
    RolledBack = 4,
    Cancelled = 5,
    /// <summary>Reserved for v2 async health verification — never written by v1.</summary>
    HealthChecking = 6,
}

public enum DeploymentStrategyDto
{
    Direct = 0,
    BlueGreen = 1,
    Canary = 2,
    Rolling = 3,
}

public enum DeploymentTriggerDto
{
    Manual = 0,
    Pipeline = 1,
    AutoPromote = 2,
}

public enum ApprovalStatusDto
{
    Pending = 0,
    Approved = 1,
    Rejected = 2,
}

// --- Read-side DTOs ---

/// <summary>
/// One row from the deployments list. Detail view returns the richer
/// <see cref="DeploymentDetailDto"/> with cascade siblings, approvals,
/// events, and secret bindings.
/// </summary>
public sealed record DeploymentSummaryDto(
    Guid Id,
    Guid ReleaseId,
    string ReleaseSemanticVersion,
    Guid DeployableUnitId,
    string DeployableUnitName,
    Guid EnvironmentId,
    string EnvironmentName,
    Guid? TargetId,
    string? TargetResourceId,
    Guid? ParentDeploymentId,
    DeploymentStatusDto Status,
    DeploymentStrategyDto Strategy,
    DeploymentTriggerDto Trigger,
    string TriggeredByPrincipal,
    DateTimeOffset? StartedAtUtc,
    DateTimeOffset? CompletedAtUtc);

public sealed record DeploymentDetailDto(
    DeploymentSummaryDto Head,
    string? FailureReason,
    string? CancellationReason,
    string? SkipPromotionPathReason,
    string? OverrideFreezeReason,
    Guid? RolledBackByDeploymentId,
    IReadOnlyList<DeploymentSummaryDto> Children,
    IReadOnlyList<ApprovalDto> Approvals,
    IReadOnlyList<DeploymentEventDto> Events,
    IReadOnlyList<DeploymentSecretBindingDto> SecretBindings);

public sealed record ApprovalDto(
    Guid Id,
    Guid DeploymentId,
    string ApproverPrincipal,
    ApprovalStatusDto Status,
    DateTimeOffset? DecidedAtUtc,
    string? Comment);

public sealed record DeploymentEventDto(
    Guid Id,
    Guid DeploymentId,
    DateTimeOffset Timestamp,
    string EventType,
    string? Detail);

public sealed record DeploymentSecretBindingDto(
    Guid DeploymentId,
    Guid ConfigurationSettingId,
    string ConfigurationKey,
    string ResolvedSecretUri,
    DateTimeOffset ResolvedAtUtc);

// --- Q1' (effective versions per (service, target) in an environment) ---

public sealed record EffectiveVersionRow(
    Guid ServiceId,
    string ServiceName,
    Guid TargetId,
    string TargetResourceId,
    string Region,
    Guid? RunningReleaseId,
    string? SemanticVersion,
    DateTimeOffset? CompletedAtUtc);

// --- Write-side requests ---

public sealed record StartDeploymentRequest(
    Guid ReleaseId,
    Guid EnvironmentId,
    IReadOnlyList<Guid> TargetIds,
    DeploymentStrategyDto Strategy,
    DeploymentTriggerDto Trigger,
    string TriggeredByPrincipal,
    string? SkipPromotionPathReason,
    string? OverrideFreezeReason);

public sealed record StartedDeploymentDto(
    Guid? ParentDeploymentId,
    IReadOnlyList<Guid> ChildDeploymentIds);

public sealed record ApproveDeploymentRequest(
    Guid ApprovalId,
    string ApproverPrincipal,
    ApprovalStatusDto Verdict,
    string? Comment);

public sealed record CancelDeploymentRequest(string CancellationReason);
