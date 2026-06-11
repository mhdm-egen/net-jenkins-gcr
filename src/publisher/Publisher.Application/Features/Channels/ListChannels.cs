using Publisher.Contracts.Channels;

namespace Publisher.Application.Features.Channels;

/// <summary>Read-model port over publishable channels (current binding + history).</summary>
public interface IChannelReader
{
    Task<IReadOnlyList<PublishChannelDto>> ListAsync(CancellationToken cancellationToken = default);
    Task<PublishChannelDto?> GetByNameAsync(string name, CancellationToken cancellationToken = default);
}

public sealed record ListChannelsQuery;

public sealed class ListChannelsHandler
{
    private readonly IChannelReader _reader;
    public ListChannelsHandler(IChannelReader reader) => _reader = reader;

    public Task<IReadOnlyList<PublishChannelDto>> HandleAsync(ListChannelsQuery query, CancellationToken cancellationToken = default)
        => _reader.ListAsync(cancellationToken);
}

public sealed record GetChannelByNameQuery(string Name);

public sealed class GetChannelByNameHandler
{
    private readonly IChannelReader _reader;
    public GetChannelByNameHandler(IChannelReader reader) => _reader = reader;

    public Task<PublishChannelDto?> HandleAsync(GetChannelByNameQuery query, CancellationToken cancellationToken = default)
        => _reader.GetByNameAsync(query.Name, cancellationToken);
}
