using Deployment.Application.Features.Catalog.ContainerImages;
using Deployment.Contracts.Catalog;
using Microsoft.EntityFrameworkCore;

namespace Deployment.Infrastructure.Persistence.Readers;

internal sealed class EfContainerImageCatalogReader : IContainerImageCatalogReader
{
    private readonly DeploymentDbContext _db;

    public EfContainerImageCatalogReader(DeploymentDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<ContainerImageDto>> ListAsync(
        bool? onlyActive,
        CancellationToken cancellationToken = default)
    {
        var query =
            from c in _db.ContainerImages.AsNoTracking()
            where !onlyActive.HasValue || c.IsActive == onlyActive.Value
            orderby c.Registry, c.Repository, c.Name
            select new ContainerImageDto(
                c.Id, c.Registry, c.Repository, c.Name, c.DefaultTag,
                c.Registry + "/" + c.Repository + "/" + c.Name,
                c.IsActive, c.CreatedAtUtc);

        return await query.ToListAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<ContainerImageDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await (
            from c in _db.ContainerImages.AsNoTracking()
            where c.Id == id
            select new ContainerImageDto(
                c.Id, c.Registry, c.Repository, c.Name, c.DefaultTag,
                c.Registry + "/" + c.Repository + "/" + c.Name,
                c.IsActive, c.CreatedAtUtc)
        ).FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
    }
}
