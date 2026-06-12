using Microsoft.EntityFrameworkCore;
using Publisher.Domain.Channels;

namespace Publisher.Infrastructure.Persistence.Repositories;

internal sealed class PublishChannelRepository
    : EfRepository<PublishChannel, Guid>, IPublishChannelRepository
{
    public PublishChannelRepository(PublisherDbContext db) : base(db) { }

    public Task<PublishChannel?> FindByNameAsync(string name, CancellationToken cancellationToken = default)
    {
        var n = name.Trim();
        // Bindings AutoInclude → the history loads with the aggregate.
        return Set.FirstOrDefaultAsync(c => c.Name == n, cancellationToken);
    }
}
