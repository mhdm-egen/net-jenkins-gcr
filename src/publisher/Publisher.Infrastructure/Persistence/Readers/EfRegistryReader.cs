using Microsoft.EntityFrameworkCore;
using Publisher.Application.Features.Registries;
using Publisher.Contracts.Registries;

namespace Publisher.Infrastructure.Persistence.Readers;

internal sealed class EfRegistryReader : IRegistryReader
{
    private readonly PublisherDbContext _db;
    public EfRegistryReader(PublisherDbContext db) => _db = db;

    public async Task<IReadOnlyList<RemoteRegistryDto>> ListAsync(CancellationToken cancellationToken = default)
        => await _db.Registries.AsNoTracking()
            .OrderBy(r => r.Name)
            .Select(r => new RemoteRegistryDto(
                r.Id, r.Name, (RegistryProviderDto)(int)r.Provider, r.RegistryHost, r.RepositoryPath,
                (RegistryAuthMethodDto)(int)r.AuthMethod, r.Username, r.CredentialSecretRef,
                r.IsDefault, r.Enabled, r.CreatedAtUtc, r.UpdatedAtUtc))
            .ToListAsync(cancellationToken).ConfigureAwait(false);

    public async Task<RemoteRegistryDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => await _db.Registries.AsNoTracking()
            .Where(r => r.Id == id)
            .Select(r => new RemoteRegistryDto(
                r.Id, r.Name, (RegistryProviderDto)(int)r.Provider, r.RegistryHost, r.RepositoryPath,
                (RegistryAuthMethodDto)(int)r.AuthMethod, r.Username, r.CredentialSecretRef,
                r.IsDefault, r.Enabled, r.CreatedAtUtc, r.UpdatedAtUtc))
            .FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
}
