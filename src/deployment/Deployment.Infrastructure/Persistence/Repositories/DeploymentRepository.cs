using Deployment.Domain.Deployments;
using Microsoft.EntityFrameworkCore;
using DeploymentRow = Deployment.Domain.Deployments.Deployment;

namespace Deployment.Infrastructure.Persistence.Repositories;

internal sealed class DeploymentRepository : EfRepository<DeploymentRow, Guid>, IDeploymentRepository
{
    public DeploymentRepository(DeploymentDbContext db) : base(db) { }

    public override Task<DeploymentRow?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => Set
            .Include(d => d.SecretBindings)
            .Include(d => d.Approvals)
            .FirstOrDefaultAsync(d => d.Id == id, cancellationToken);

    public Task<DeploymentRow?> FindLatestSucceededAsync(
        Guid deployableUnitId,
        Guid environmentId,
        CancellationToken cancellationToken = default)
        => Db.Deployments
            .Join(Db.Releases,
                d => d.ReleaseId,
                r => r.Id,
                (d, r) => new { d, r })
            .Where(x => x.r.DeployableUnitId == deployableUnitId
                        && x.d.EnvironmentId == environmentId
                        && x.d.Status == DeploymentStatus.Succeeded)
            .OrderByDescending(x => x.d.CompletedAtUtc)
            .Select(x => x.d)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<DeploymentRow>> GetCascadeAsync(
        Guid parentDeploymentId,
        CancellationToken cancellationToken = default)
    {
        return await Set
            .Where(d => d.Id == parentDeploymentId || d.ParentDeploymentId == parentDeploymentId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }
}
