using Deployment.Domain.Environments;
using Microsoft.EntityFrameworkCore;
using Environment = Deployment.Domain.Environments.Environment;

namespace Deployment.Infrastructure.Persistence.Repositories;

internal sealed class EnvironmentRepository : EfRepository<Environment, Guid>, IEnvironmentRepository
{
    public EnvironmentRepository(DeploymentDbContext db) : base(db) { }

    public Task<Environment?> FindByNameAsync(string name, CancellationToken cancellationToken = default)
        => Set.FirstOrDefaultAsync(e => e.Name == name, cancellationToken);

    public Task<Environment?> FindByPromotionRankAsync(int targetRank, CancellationToken cancellationToken = default)
        => Set.FirstOrDefaultAsync(e => e.PromotionRank == targetRank, cancellationToken);
}
