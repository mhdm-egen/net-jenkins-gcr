using Deployment.Application.Features.Catalog.Services;
using Deployment.Contracts.Catalog;
using Microsoft.EntityFrameworkCore;

namespace Deployment.Infrastructure.Persistence.Readers;

internal sealed class EfServiceCatalogReader : IServiceCatalogReader
{
    private readonly DeploymentDbContext _db;

    public EfServiceCatalogReader(DeploymentDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<ServiceDto>> ListAsync(
        bool? onlyActive,
        CancellationToken cancellationToken = default)
    {
        var query =
            from s in _db.Services.AsNoTracking()
            join u in _db.DeployableUnits.AsNoTracking() on s.Id equals u.Id
            where !onlyActive.HasValue || u.IsActive == onlyActive.Value
            orderby u.Name
            select new ServiceDto(
                s.Id,
                u.Name,
                (ServiceKindDto)(int)s.Kind,
                s.RepositoryUrl,
                s.TargetFramework,
                u.IsActive,
                u.CreatedAtUtc);

        return await query.ToListAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<ServiceDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await (
            from s in _db.Services.AsNoTracking()
            join u in _db.DeployableUnits.AsNoTracking() on s.Id equals u.Id
            where s.Id == id
            select new ServiceDto(
                s.Id, u.Name,
                (ServiceKindDto)(int)s.Kind,
                s.RepositoryUrl, s.TargetFramework,
                u.IsActive, u.CreatedAtUtc)
        ).FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
    }
}
