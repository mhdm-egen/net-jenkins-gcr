using Microsoft.EntityFrameworkCore;
using Publisher.Domain.Containers;

namespace Publisher.Infrastructure.Persistence.Repositories;

public sealed class PublishableContainerRepository
    : EfRepository<PublishableContainer, Guid>, IPublishableContainerRepository
{
    public PublishableContainerRepository(PublisherDbContext db) : base(db) { }

    public Task<PublishableContainer?> FindByNaturalKeyAsync(
        Guid repositoryId,
        string containerName,
        string version,
        CancellationToken cancellationToken = default)
    {
        var name = containerName.Trim();
        var ver = version?.Trim() ?? string.Empty;
        return Set.FirstOrDefaultAsync(
            c => c.RepositoryId == repositoryId && c.ContainerName == name && c.Version == ver,
            cancellationToken);
    }
}
