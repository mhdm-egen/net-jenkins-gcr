using Jenkins.Domain.Abstractions;
using Jenkins.Domain.Common;
using Microsoft.EntityFrameworkCore;

namespace Jenkins.Infrastructure.Persistence.Repositories;

/// <summary>
/// Generic EF-Core-backed repository. Concrete repositories derive from this and
/// add aggregate-specific lookups. The persistence flush is owned by
/// <see cref="UnitOfWork"/> — repositories only mutate the change-tracker.
/// </summary>
public abstract class EfRepository<TAggregate, TId> : IRepository<TAggregate, TId>
    where TAggregate : AggregateRoot<TId>
    where TId : notnull
{
    protected readonly JenkinsCiDbContext Db;

    protected EfRepository(JenkinsCiDbContext db)
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
