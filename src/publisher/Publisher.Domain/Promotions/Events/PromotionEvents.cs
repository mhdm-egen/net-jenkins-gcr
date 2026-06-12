using Publisher.Domain.Common;

namespace Publisher.Domain.Promotions.Events;

/// <summary>A promotion was requested — drives the executor that runs the actual registry copy.</summary>
public sealed record PromotionRequested(
    Guid PromotionId,
    Guid ContainerId,
    Guid RegistryId,
    string SourceRef,
    string RemoteRef,
    DateTimeOffset OccurredAtUtc) : IDomainEvent;

/// <summary>The image copy completed — translated to the Publisher.ContainerPromoted integration event.</summary>
public sealed record PromotionSucceeded(
    Guid PromotionId,
    Guid ContainerId,
    Guid RegistryId,
    string RegistryName,
    Guid RepositoryId,
    string ContainerName,
    string Version,
    string SourceRef,
    string RemoteRef,
    DateTimeOffset OccurredAtUtc) : IDomainEvent;

public sealed record PromotionFailed(
    Guid PromotionId,
    Guid ContainerId,
    Guid RegistryId,
    string Reason,
    DateTimeOffset OccurredAtUtc) : IDomainEvent;
