using Jenkins.Domain.Common;

namespace Jenkins.Domain.Handoffs.Events;

public sealed record HandoffRequested(
    Guid HandoffId,
    Guid BuildId,
    Guid BuildArtifactId,
    Guid DeployableComponentId,
    Guid DeployableUnitId,
    string SemanticVersion,
    string ArtifactUri,
    string RequestedByPrincipal,
    DateTimeOffset OccurredAtUtc) : IDomainEvent;

public sealed record HandoffPublished(
    Guid HandoffId,
    Guid DeploymentReleaseId,
    DateTimeOffset OccurredAtUtc) : IDomainEvent;

public sealed record HandoffFailed(
    Guid HandoffId,
    string FailureReason,
    DateTimeOffset OccurredAtUtc) : IDomainEvent;

public sealed record HandoffSkipped(
    Guid HandoffId,
    string Reason,
    DateTimeOffset OccurredAtUtc) : IDomainEvent;
