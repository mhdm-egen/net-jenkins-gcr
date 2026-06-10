using Deployment.Domain.Abstractions;
using Deployment.Domain.Common;
using Microsoft.EntityFrameworkCore;

namespace Deployment.Infrastructure.Persistence.Repositories;

/// <summary>
/// Generic EF-Core-backed repository. Concrete repositories derive from this
/// and add aggregate-specific lookup helpers (e.g. <c>FindByNameAsync</c>).
/// The persistence flush is owned by <see cref="UnitOfWork"/> — repositories
/// only mutate the change-tracker.
/// </summary>
internal abstract class EfRepository<TAggregate, TId> : IRepository<TAggregate, TId>
    where TAggregate : AggregateRoot<TId>
    where TId : notnull
{
    protected readonly DeploymentDbContext Db;

    protected EfRepository(DeploymentDbContext db)
    {
        Db = db;
    }

    protected DbSet<TAggregate> Set => Db.Set<TAggregate>();

    public virtual Task<TAggregate?> GetByIdAsync(TId id, CancellationToken cancellationToken = default)
        => Set.FirstOrDefaultAsync(a => a.Id.Equals(id), cancellationToken);

    public async Task AddAsync(TAggregate aggregate, CancellationToken cancellationToken = default)
        => await Set.AddAsync(aggregate, cancellationToken).ConfigureAwait(false);

    public void Remove(TAggregate aggregate) => Set.Remove(aggregate);
}
