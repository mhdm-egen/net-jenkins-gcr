using Deployment.Domain.Common;

namespace Deployment.Domain.AspireApps.Runs.Events;

/// <summary>A run was requested — drives the executor that shells out to aspirate.</summary>
public sealed record AspireApplicationRunRequested(Guid RunId, Guid ApplicationId, DateTimeOffset OccurredAtUtc) : IDomainEvent;

public sealed record AspireApplicationRunSucceeded(
    Guid RunId, Guid ApplicationId, string ApplicationName, string Namespace, DateTimeOffset OccurredAtUtc) : IDomainEvent;

public sealed record AspireApplicationRunFailed(
    Guid RunId, Guid ApplicationId, string ApplicationName, string Reason, DateTimeOffset OccurredAtUtc) : IDomainEvent;
