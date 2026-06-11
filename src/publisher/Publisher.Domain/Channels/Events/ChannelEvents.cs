using Publisher.Domain.Common;

namespace Publisher.Domain.Channels.Events;

/// <summary>A new publishable name (channel) was created and bound to its first container.</summary>
public sealed record PublishChannelCreated(
    Guid ChannelId,
    string Name,
    Guid ContainerId,
    string BoundBy,
    DateTimeOffset OccurredAtUtc) : IDomainEvent;

/// <summary>
/// A container was tagged publishable under a channel name — either the channel's first binding
/// or a move of an existing channel's pointer. <c>PreviousContainerId</c> is null on first bind.
/// </summary>
public sealed record ContainerTaggedPublishable(
    Guid ChannelId,
    string Name,
    Guid ContainerId,
    Guid? PreviousContainerId,
    string BoundBy,
    DateTimeOffset OccurredAtUtc) : IDomainEvent;
