using Publisher.Domain.Channels.Events;
using Publisher.Domain.Common;

namespace Publisher.Domain.Channels;

/// <summary>
/// A "publishable name" — a stable, well-defined alias (e.g. <c>web-api-stable</c>,
/// <c>prod-candidate</c>) that points at exactly one <see cref="Containers.PublishableContainer"/>
/// at a time. Tagging a container as publishable binds it to a channel; re-tagging a newer
/// container under the same name <i>moves</i> the pointer (mutable alias, like a docker tag or a
/// release channel). The full pointer history is retained as <see cref="ChannelBinding"/> children.
///
/// Callers later refer to the channel name to decide what to promote to the remote registry —
/// "publish whatever <c>stable</c> points at now."
/// </summary>
public sealed class PublishChannel : AggregateRoot<Guid>
{
    private readonly List<ChannelBinding> _bindings = new();

    /// <summary>The well-defined publishable name. Unique across the publisher.</summary>
    public string Name { get; private set; }

    /// <summary>The container the channel currently points at.</summary>
    public Guid CurrentContainerId { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }

    /// <summary>Pointer history, oldest first. The last entry is the current binding.</summary>
    public IReadOnlyList<ChannelBinding> Bindings => _bindings.AsReadOnly();

    private PublishChannel() => Name = string.Empty;

    private PublishChannel(Guid id, string name, Guid containerId, string boundBy, DateTimeOffset at)
    {
        Id = id;
        Name = name;
        CurrentContainerId = containerId;
        CreatedAtUtc = at;
        UpdatedAtUtc = at;
        _bindings.Add(new ChannelBinding(Guid.NewGuid(), id, 1, containerId, boundBy, at));
    }

    /// <summary>
    /// Creates a new channel bound to its first container. Use when no channel of this name exists yet.
    /// </summary>
    public static PublishChannel Create(Guid id, string name, Guid containerId, string boundBy, DateTimeOffset at)
    {
        if (id == Guid.Empty) throw new ArgumentException("Id cannot be empty.", nameof(id));
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Name cannot be empty.", nameof(name));
        if (containerId == Guid.Empty) throw new ArgumentException("ContainerId cannot be empty.", nameof(containerId));

        var channel = new PublishChannel(id, name.Trim(), containerId, boundBy, at);
        channel.RaiseEvent(new PublishChannelCreated(id, channel.Name, containerId, channel.CurrentBoundBy, at));
        channel.RaiseEvent(new ContainerTaggedPublishable(id, channel.Name, containerId, PreviousContainerId: null, channel.CurrentBoundBy, at));
        return channel;
    }

    /// <summary>
    /// Moves the channel to point at a different container. Idempotent — does nothing if the
    /// channel already points at <paramref name="containerId"/>.
    /// </summary>
    public void Bind(Guid containerId, string boundBy, DateTimeOffset at)
    {
        if (containerId == Guid.Empty) throw new ArgumentException("ContainerId cannot be empty.", nameof(containerId));
        if (containerId == CurrentContainerId) return;

        var previous = CurrentContainerId;
        var nextSeq = _bindings.Count == 0 ? 1 : _bindings[^1].Sequence + 1;
        _bindings.Add(new ChannelBinding(Guid.NewGuid(), Id, nextSeq, containerId, boundBy, at));
        CurrentContainerId = containerId;
        UpdatedAtUtc = at;

        RaiseEvent(new ContainerTaggedPublishable(Id, Name, containerId, previous, CurrentBoundBy, at));
    }

    private string CurrentBoundBy => _bindings.Count == 0 ? "system" : _bindings[^1].BoundBy;
}
