using Microsoft.EntityFrameworkCore;
using Publisher.Application.Features.Containers;
using Publisher.Contracts.Containers;

namespace Publisher.Infrastructure.Persistence.Readers;

internal sealed class EfContainerInventoryReader : IContainerInventoryReader
{
    private readonly PublisherDbContext _db;

    public EfContainerInventoryReader(PublisherDbContext db) => _db = db;

    public async Task<IReadOnlyList<PublishableContainerDto>> ListAsync(
        Guid? repositoryId,
        string? containerName,
        CancellationToken cancellationToken = default)
    {
        var name = string.IsNullOrWhiteSpace(containerName) ? null : containerName.Trim();

        var query =
            from c in _db.Containers.AsNoTracking()
            where (!repositoryId.HasValue || c.RepositoryId == repositoryId.Value)
                  && (name == null || c.ContainerName == name)
            orderby c.LastSeenAtUtc descending
            select new PublishableContainerDto(
                c.Id, c.RepositoryId, c.BuildId, c.ContainerName, c.Version,
                c.CommitSha, c.ArtifactUri, c.ImageDigest, c.IsActive, (ContainerSourceDto)(int)c.Source,
                c.FirstSeenAtUtc, c.LastSeenAtUtc);

        return await query.ToListAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<PublishableContainerDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await (
            from c in _db.Containers.AsNoTracking()
            where c.Id == id
            select new PublishableContainerDto(
                c.Id, c.RepositoryId, c.BuildId, c.ContainerName, c.Version,
                c.CommitSha, c.ArtifactUri, c.ImageDigest, c.IsActive, (ContainerSourceDto)(int)c.Source,
                c.FirstSeenAtUtc, c.LastSeenAtUtc)
        ).FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
    }
}
