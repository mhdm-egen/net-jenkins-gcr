using Deployment.Application.Features.Catalog.Applications;
using Deployment.Contracts.Catalog;
using Microsoft.EntityFrameworkCore;

namespace Deployment.Infrastructure.Persistence.Readers;

internal sealed class EfApplicationCatalogReader : IApplicationCatalogReader
{
    private readonly DeploymentDbContext _db;

    public EfApplicationCatalogReader(DeploymentDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<ApplicationDto>> ListAsync(
        bool? onlyActive,
        CancellationToken cancellationToken = default)
    {
        var heads = await (
            from a in _db.Applications.AsNoTracking()
            join u in _db.DeployableUnits.AsNoTracking() on a.Id equals u.Id
            where !onlyActive.HasValue || u.IsActive == onlyActive.Value
            orderby u.Name
            select new
            {
                a.Id, u.Name, a.Description, u.IsActive, u.CreatedAtUtc,
            }
        ).ToListAsync(cancellationToken).ConfigureAwait(false);

        if (heads.Count == 0) return Array.Empty<ApplicationDto>();

        var appIds = heads.Select(h => h.Id).ToList();

        // Pull membership rows with denormalized service names in one round-trip.
        var members = await (
            from aps in _db.ApplicationServices.AsNoTracking()
            join u in _db.DeployableUnits.AsNoTracking() on aps.ServiceId equals u.Id
            where appIds.Contains(aps.ApplicationId)
            orderby aps.DeploymentOrder, u.Name
            select new
            {
                aps.ApplicationId,
                aps.ServiceId,
                ServiceName = u.Name,
                aps.Role,
                aps.IsOptional,
                aps.DeploymentOrder,
            }
        ).ToListAsync(cancellationToken).ConfigureAwait(false);

        var byApp = members
            .GroupBy(m => m.ApplicationId)
            .ToDictionary(
                g => g.Key,
                g => (IReadOnlyList<ApplicationServiceMemberDto>)g
                    .Select(m => new ApplicationServiceMemberDto(
                        m.ServiceId, m.ServiceName, m.Role, m.IsOptional, m.DeploymentOrder))
                    .ToList());

        return heads
            .Select(h => new ApplicationDto(
                h.Id, h.Name, h.Description, h.IsActive, h.CreatedAtUtc,
                byApp.TryGetValue(h.Id, out var ms) ? ms : Array.Empty<ApplicationServiceMemberDto>()))
            .ToList();
    }

    public async Task<ApplicationDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var head = await (
            from a in _db.Applications.AsNoTracking()
            join u in _db.DeployableUnits.AsNoTracking() on a.Id equals u.Id
            where a.Id == id
            select new
            {
                a.Id, u.Name, a.Description, u.IsActive, u.CreatedAtUtc,
            }
        ).FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);

        if (head is null) return null;

        var members = await (
            from aps in _db.ApplicationServices.AsNoTracking()
            join u in _db.DeployableUnits.AsNoTracking() on aps.ServiceId equals u.Id
            where aps.ApplicationId == id
            orderby aps.DeploymentOrder, u.Name
            select new ApplicationServiceMemberDto(
                aps.ServiceId, u.Name, aps.Role, aps.IsOptional, aps.DeploymentOrder)
        ).ToListAsync(cancellationToken).ConfigureAwait(false);

        return new ApplicationDto(head.Id, head.Name, head.Description,
            head.IsActive, head.CreatedAtUtc, members);
    }
}
