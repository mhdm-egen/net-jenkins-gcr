using Deployment.Domain.Common;

namespace Deployment.Domain.DeployableUnits.Events;

public sealed record ServiceRegistered(
    Guid ServiceId,
    string Name,
    ServiceKind Kind,
    string RepositoryUrl,
    string TargetFramework,
    DateTimeOffset OccurredAtUtc) : IDomainEvent;

public sealed record ServiceRenamed(
    Guid ServiceId,
    string OldName,
    string NewName,
    DateTimeOffset OccurredAtUtc) : IDomainEvent;

public sealed record ServiceRepositoryInfoUpdated(
    Guid ServiceId,
    string RepositoryUrl,
    string TargetFramework,
    DateTimeOffset OccurredAtUtc) : IDomainEvent;

public sealed record ServiceDeactivated(
    Guid ServiceId,
    DateTimeOffset OccurredAtUtc) : IDomainEvent;

public sealed record ServiceReactivated(
    Guid ServiceId,
    DateTimeOffset OccurredAtUtc) : IDomainEvent;
