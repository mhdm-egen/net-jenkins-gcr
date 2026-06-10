using Deployment.Domain.Common;

namespace Deployment.Domain.Environments.Events;

public sealed record EnvironmentRegistered(
    Guid EnvironmentId,
    string Name,
    int PromotionRank,
    bool RequiresApproval,
    bool IsProduction,
    DateTimeOffset OccurredAtUtc) : IDomainEvent;

public sealed record EnvironmentRenamed(
    Guid EnvironmentId,
    string OldName,
    string NewName,
    DateTimeOffset OccurredAtUtc) : IDomainEvent;

public sealed record EnvironmentPromotionRankChanged(
    Guid EnvironmentId,
    int OldRank,
    int NewRank,
    DateTimeOffset OccurredAtUtc) : IDomainEvent;

public sealed record EnvironmentApprovalRequirementChanged(
    Guid EnvironmentId,
    bool RequiresApproval,
    DateTimeOffset OccurredAtUtc) : IDomainEvent;

public sealed record EnvironmentMarkedProduction(
    Guid EnvironmentId,
    bool IsProduction,
    DateTimeOffset OccurredAtUtc) : IDomainEvent;

public sealed record DeploymentTargetAdded(
    Guid EnvironmentId,
    Guid TargetId,
    TargetKind TargetKind,
    string ResourceId,
    string Region,
    string? Slot,
    DateTimeOffset OccurredAtUtc) : IDomainEvent;

public sealed record DeploymentTargetUpdated(
    Guid EnvironmentId,
    Guid TargetId,
    TargetKind TargetKind,
    string ResourceId,
    string Region,
    string? Slot,
    DateTimeOffset OccurredAtUtc) : IDomainEvent;

public sealed record DeploymentTargetRemoved(
    Guid EnvironmentId,
    Guid TargetId,
    DateTimeOffset OccurredAtUtc) : IDomainEvent;

public sealed record FreezeWindowScheduled(
    Guid EnvironmentId,
    Guid FreezeWindowId,
    DateTimeOffset StartUtc,
    DateTimeOffset EndUtc,
    string Reason,
    string CreatedByPrincipal,
    DateTimeOffset OccurredAtUtc) : IDomainEvent;

public sealed record FreezeWindowCancelled(
    Guid EnvironmentId,
    Guid FreezeWindowId,
    DateTimeOffset OccurredAtUtc) : IDomainEvent;
