using Deployment.Domain.Common;

namespace Deployment.Domain.DeployableUnits.Events;

public sealed record ApplicationRegistered(
    Guid ApplicationId,
    string Name,
    string Description,
    DateTimeOffset OccurredAtUtc) : IDomainEvent;

public sealed record ApplicationRenamed(
    Guid ApplicationId,
    string OldName,
    string NewName,
    DateTimeOffset OccurredAtUtc) : IDomainEvent;

public sealed record ApplicationDescriptionChanged(
    Guid ApplicationId,
    string NewDescription,
    DateTimeOffset OccurredAtUtc) : IDomainEvent;

public sealed record ApplicationDeactivated(
    Guid ApplicationId,
    DateTimeOffset OccurredAtUtc) : IDomainEvent;

public sealed record ApplicationReactivated(
    Guid ApplicationId,
    DateTimeOffset OccurredAtUtc) : IDomainEvent;

public sealed record ServiceAddedToApplication(
    Guid ApplicationId,
    Guid ServiceId,
    string Role,
    bool IsOptional,
    int DeploymentOrder,
    DateTimeOffset OccurredAtUtc) : IDomainEvent;

public sealed record ServiceRemovedFromApplication(
    Guid ApplicationId,
    Guid ServiceId,
    DateTimeOffset OccurredAtUtc) : IDomainEvent;

public sealed record ApplicationServiceMembershipUpdated(
    Guid ApplicationId,
    Guid ServiceId,
    string Role,
    bool IsOptional,
    int DeploymentOrder,
    DateTimeOffset OccurredAtUtc) : IDomainEvent;
