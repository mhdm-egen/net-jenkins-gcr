using Deployment.Domain.Common;

namespace Deployment.Domain.ContainerImages.Events;

public sealed record ContainerImageRegistered(
    Guid ContainerImageId,
    string Registry,
    string Repository,
    string Name,
    string DefaultTag,
    DateTimeOffset OccurredAtUtc) : IDomainEvent;

public sealed record ContainerImageDefaultTagChanged(
    Guid ContainerImageId,
    string DefaultTag,
    DateTimeOffset OccurredAtUtc) : IDomainEvent;

public sealed record ContainerImageDeactivated(
    Guid ContainerImageId,
    DateTimeOffset OccurredAtUtc) : IDomainEvent;

public sealed record ContainerImageReactivated(
    Guid ContainerImageId,
    DateTimeOffset OccurredAtUtc) : IDomainEvent;
