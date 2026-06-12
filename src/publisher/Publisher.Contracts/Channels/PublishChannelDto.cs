namespace Publisher.Contracts.Channels;

/// <summary>
/// Wire shape of a publishable name (channel): the current binding plus a count of how
/// many times it has moved. <see cref="ChannelBindingDto"/> rows carry the full history.
/// </summary>
public sealed record PublishChannelDto(
    Guid Id,
    string Name,
    Guid CurrentContainerId,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    IReadOnlyList<ChannelBindingDto> History);

/// <summary>One pointer-history entry for a channel.</summary>
public sealed record ChannelBindingDto(
    int Sequence,
    Guid ContainerId,
    string BoundBy,
    DateTimeOffset BoundAtUtc);
