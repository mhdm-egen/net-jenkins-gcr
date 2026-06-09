using Deployment.Domain.Common;

namespace Deployment.Domain.Abstractions;

/// <summary>
/// Repository contract per aggregate root. Concrete repositories live in
/// <c>Deployment.Infrastructure.Persistence.Repositories</c> and target EF Core;
/// the Application layer depends only on this abstraction.
/// </summary>
public interface IRepository<TAggregate, in TId>
    where TAggregate : AggregateRoot<TId>
    where TId : notnull
{
    Task<TAggregate?> GetByIdAsync(TId id, CancellationToken cancellationToken = default);
    Task AddAsync(TAggregate aggregate, CancellationToken cancellationToken = default);
    void Remove(TAggregate aggregate);
}
