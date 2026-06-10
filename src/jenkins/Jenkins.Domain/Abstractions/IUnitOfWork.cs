namespace Jenkins.Domain.Abstractions;

/// <summary>
/// Transactional save boundary for one or more aggregates. The Infrastructure
/// implementation flushes the EF Core change tracker and dispatches collected
/// domain events through Wolverine's in-process bus.
/// </summary>
public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
