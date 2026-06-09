using Deployment.Domain.Abstractions;

namespace Deployment.Domain.DeployableUnits;

/// <summary>
/// Persistence seam for the <see cref="Application"/> aggregate. Includes the
/// <see cref="ApplicationService"/> child collection by default when fetched
/// by id — the membership rows are part of the aggregate's invariants and
/// shouldn't be lazily loaded.
/// </summary>
public interface IApplicationRepository : IRepository<Application, Guid>
{
    Task<Application?> FindByNameAsync(string name, CancellationToken cancellationToken = default);
}
