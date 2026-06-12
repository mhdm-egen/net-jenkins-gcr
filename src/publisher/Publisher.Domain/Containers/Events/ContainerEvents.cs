using Publisher.Domain.Common;

namespace Publisher.Domain.Containers.Events;

/// <summary>A container image was recorded in the publisher's inventory for the first time.</summary>
public sealed record ContainerRecorded(
    Guid ContainerId,
    Guid RepositoryId,
    Guid BuildId,
    string ContainerName,
    string Version,
    string ArtifactUri,
    string? ImageDigest,
    DateTimeOffset OccurredAtUtc) : IDomainEvent;

/// <summary>An already-known container was re-observed with a changed reference/digest.</summary>
public sealed record ContainerReferenceUpdated(
    Guid ContainerId,
    string ArtifactUri,
    string? ImageDigest,
    DateTimeOffset OccurredAtUtc) : IDomainEvent;

/// <summary>A container was activated or deactivated for promotion.</summary>
public sealed record ContainerActivationChanged(
    Guid ContainerId,
    bool IsActive,
    DateTimeOffset OccurredAtUtc) : IDomainEvent;
