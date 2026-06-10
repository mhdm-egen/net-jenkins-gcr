using Deployment.Domain.DeployableUnits;
using Microsoft.EntityFrameworkCore;
using DeployableApplication = Deployment.Domain.DeployableUnits.Application;

namespace Deployment.Infrastructure.Persistence.Repositories;

internal sealed class ServiceRepository : EfRepository<Service, Guid>, IServiceRepository
{
    public ServiceRepository(DeploymentDbContext db) : base(db) { }

    public override Task<Service?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => Set.Include(s => s.Unit).FirstOrDefaultAsync(s => s.Id == id, cancellationToken);

    public Task<Service?> FindByNameAsync(string name, CancellationToken cancellationToken = default)
        => Set.Include(s => s.Unit).FirstOrDefaultAsync(s => s.Unit.Name == name, cancellationToken);
}

internal sealed class ApplicationRepository
    : EfRepository<DeployableApplication, Guid>, IApplicationRepository
{
    public ApplicationRepository(DeploymentDbContext db) : base(db) { }

    public override Task<DeployableApplication?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => Set.Include(a => a.Unit).FirstOrDefaultAsync(a => a.Id == id, cancellationToken);

    public Task<DeployableApplication?> FindByNameAsync(string name, CancellationToken cancellationToken = default)
        => Set.Include(a => a.Unit).FirstOrDefaultAsync(a => a.Unit.Name == name, cancellationToken);
}
