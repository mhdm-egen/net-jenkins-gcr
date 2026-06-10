using Deployment.Domain.Releases;
using Microsoft.EntityFrameworkCore;

namespace Deployment.Infrastructure.Persistence.Repositories;

internal sealed class ReleaseRepository : EfRepository<Release, Guid>, IReleaseRepository
{
    public ReleaseRepository(DeploymentDbContext db) : base(db) { }

    public Task<Release?> FindLatestAvailableAsync(Guid deployableUnitId, CancellationToken cancellationToken = default)
        => Set
            .Where(r => r.DeployableUnitId == deployableUnitId && r.Status == ReleaseStatus.Available)
            .OrderByDescending(r => r.CreatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

    public Task<Release?> FindByVersionAsync(
        Guid deployableUnitId,
        string semanticVersion,
        CancellationToken cancellationToken = default)
        => Set.FirstOrDefaultAsync(
            r => r.DeployableUnitId == deployableUnitId && r.SemanticVersion == semanticVersion,
            cancellationToken);
}
