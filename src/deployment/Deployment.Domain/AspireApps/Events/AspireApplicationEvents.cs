using Deployment.Domain.Common;

namespace Deployment.Domain.AspireApps.Events;

public sealed record AspireApplicationRegistered(Guid ApplicationId, string Name, DateTimeOffset OccurredAtUtc) : IDomainEvent;
public sealed record AspireApplicationUpdated(Guid ApplicationId, string Name, DateTimeOffset OccurredAtUtc) : IDomainEvent;
