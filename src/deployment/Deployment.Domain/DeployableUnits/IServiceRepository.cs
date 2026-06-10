using Deployment.Domain.Abstractions;

namespace Deployment.Domain.DeployableUnits;

/// <summary>
/// Persistence seam for the <see cref="Service"/> aggregate. Concrete impl
/// lives in <c>Deployment.Infrastructure.Persistence.Repositories</c>.
/// </summary>
public interface IServiceRepository : IRepository<Service, Guid>
{
    /// <summary>
    /// Used by <c>RegisterService</c> to enforce the unique-name invariant
    /// across the catalog (matches the unique index on <c>DeployableUnit.Name</c>).
    /// </summary>
    Task<Service?> FindByNameAsync(string name, CancellationToken cancellationToken = default);
}
