using Publisher.Domain.Abstractions;

namespace Publisher.Domain.Registries;

public interface IRemoteRegistryRepository : IRepository<RemoteRegistry, Guid>
{
    Task<RemoteRegistry?> FindByNameAsync(string name, CancellationToken cancellationToken = default);

    /// <summary>The current default registry, or null if none is marked.</summary>
    Task<RemoteRegistry?> FindDefaultAsync(CancellationToken cancellationToken = default);

    /// <summary>All registries currently flagged default (should be 0 or 1; used to clear on re-default).</summary>
    Task<IReadOnlyList<RemoteRegistry>> ListDefaultsAsync(CancellationToken cancellationToken = default);
}
