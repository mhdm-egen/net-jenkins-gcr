using Jenkins.Domain.Common;

namespace Jenkins.Domain.Abstractions;

/// <summary>
/// Repository contract per aggregate root. Concrete repositories live in
/// <c>Jenkins.Infrastructure.Persistence.Repositories</c> and target EF Core; the
/// Application layer depends only on this abstraction. Mirrors the deployment
/// service's <c>IRepository</c>.
/// </summary>
public interface IRepository<TAggregate, in TId>
    where TAggregate : AggregateRoot<TId>
    where TId : notnull
{
    Task<TAggregate?> GetByIdAsync(TId id, CancellationToken cancellationToken = default);
    Task AddAsync(TAggregate aggregate, CancellationToken cancellationToken = default);
    void Remove(TAggregate aggregate);
}
