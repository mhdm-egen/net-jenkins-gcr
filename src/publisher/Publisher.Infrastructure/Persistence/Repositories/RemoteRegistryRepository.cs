using Microsoft.EntityFrameworkCore;
using Publisher.Domain.Registries;

namespace Publisher.Infrastructure.Persistence.Repositories;

public sealed class RemoteRegistryRepository
    : EfRepository<RemoteRegistry, Guid>, IRemoteRegistryRepository
{
    public RemoteRegistryRepository(PublisherDbContext db) : base(db) { }

    public Task<RemoteRegistry?> FindByNameAsync(string name, CancellationToken cancellationToken = default)
    {
        var n = name.Trim();
        return Set.FirstOrDefaultAsync(r => r.Name == n, cancellationToken);
    }

    public Task<RemoteRegistry?> FindDefaultAsync(CancellationToken cancellationToken = default)
        => Set.FirstOrDefaultAsync(r => r.IsDefault, cancellationToken);

    public async Task<IReadOnlyList<RemoteRegistry>> ListDefaultsAsync(CancellationToken cancellationToken = default)
        => await Set.Where(r => r.IsDefault).ToListAsync(cancellationToken).ConfigureAwait(false);
}
