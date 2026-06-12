using Publisher.Domain.Common;

namespace Publisher.Domain.Channels;

/// <summary>
/// One entry in a <see cref="PublishChannel"/>'s history: the channel pointed at a given
/// container from <see cref="BoundAtUtc"/>. The highest <see cref="Sequence"/> is the current
/// binding (mirrored on the channel as <see cref="PublishChannel.CurrentContainerId"/>).
/// </summary>
public sealed class ChannelBinding : Entity<Guid>
{
    public Guid ChannelId { get; private set; }
    public int Sequence { get; private set; }
    public Guid ContainerId { get; private set; }
    public string BoundBy { get; private set; }
    public DateTimeOffset BoundAtUtc { get; private set; }

    private ChannelBinding() => BoundBy = string.Empty;

    internal ChannelBinding(Guid id, Guid channelId, int sequence, Guid containerId, string boundBy, DateTimeOffset boundAtUtc)
    {
        Id = id;
        ChannelId = channelId;
        Sequence = sequence;
        ContainerId = containerId;
        BoundBy = string.IsNullOrWhiteSpace(boundBy) ? "system" : boundBy.Trim();
        BoundAtUtc = boundAtUtc;
    }
}
