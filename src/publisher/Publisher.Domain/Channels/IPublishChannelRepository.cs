using Publisher.Domain.Abstractions;

namespace Publisher.Domain.Channels;

/// <summary>
/// Repository for the <see cref="PublishChannel"/> aggregate. Adds name lookup since the
/// channel name (not the id) is the publishable identity callers refer to.
/// </summary>
public interface IPublishChannelRepository : IRepository<PublishChannel, Guid>
{
    /// <summary>Finds a channel by its unique publishable name, or null. Loads the binding history.</summary>
    Task<PublishChannel?> FindByNameAsync(string name, CancellationToken cancellationToken = default);
}
