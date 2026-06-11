using Deployment.Domain.ContainerImages;
using Microsoft.EntityFrameworkCore;

namespace Deployment.Infrastructure.Persistence.Repositories;

internal sealed class ContainerImageRepository : EfRepository<ContainerImage, Guid>, IContainerImageRepository
{
    public ContainerImageRepository(DeploymentDbContext db) : base(db) { }

    public Task<ContainerImage?> FindByCoordinateAsync(
        string registry, string repository, string name, CancellationToken cancellationToken = default)
        => Set.FirstOrDefaultAsync(
            c => c.Registry == registry && c.Repository == repository && c.Name == name,
            cancellationToken);
}
