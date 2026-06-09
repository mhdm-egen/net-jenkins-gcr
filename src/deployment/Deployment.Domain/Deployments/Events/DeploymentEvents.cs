using Deployment.Domain.Common;

namespace Deployment.Domain.Deployments.Events;

public sealed record DeploymentQueued(
    Guid DeploymentId,
    Guid ReleaseId,
    Guid EnvironmentId,
    Guid? TargetId,
    Guid? ParentDeploymentId,
    DeploymentStrategy Strategy,
    DeploymentTrigger Trigger,
    string TriggeredByPrincipal,
    string? SkipPromotionPathReason,
    string? OverrideFreezeReason,
    DateTimeOffset OccurredAtUtc) : IDomainEvent;

public sealed record DeploymentStarted(
    Guid DeploymentId,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset OccurredAtUtc) : IDomainEvent;

public sealed record DeploymentSucceeded(
    Guid DeploymentId,
    DateTimeOffset CompletedAtUtc,
    DateTimeOffset OccurredAtUtc) : IDomainEvent;

public sealed record DeploymentFailed(
    Guid DeploymentId,
    string FailureReason,
    DateTimeOffset CompletedAtUtc,
    DateTimeOffset OccurredAtUtc) : IDomainEvent;

public sealed record DeploymentCancelled(
    Guid DeploymentId,
    string CancellationReason,
    DateTimeOffset OccurredAtUtc) : IDomainEvent;

public sealed record DeploymentRolledBack(
    Guid OriginalDeploymentId,
    Guid RollbackDeploymentId,
    DateTimeOffset OccurredAtUtc) : IDomainEvent;

public sealed record ApprovalRequested(
    Guid DeploymentId,
    Guid ApprovalId,
    string ApproverPrincipal,
    DateTimeOffset OccurredAtUtc) : IDomainEvent;

public sealed record ApprovalDecided(
    Guid DeploymentId,
    Guid ApprovalId,
    string ApproverPrincipal,
    ApprovalStatus Verdict,
    string? Comment,
    DateTimeOffset OccurredAtUtc) : IDomainEvent;

public sealed record DeploymentAuditEventRecorded(
    Guid DeploymentId,
    Guid EventId,
    string EventType,
    string? Detail,
    DateTimeOffset OccurredAtUtc) : IDomainEvent;

public sealed record DeploymentSecretBindingResolved(
    Guid DeploymentId,
    Guid ConfigurationSettingId,
    string ResolvedSecretUri,
    DateTimeOffset OccurredAtUtc) : IDomainEvent;
