namespace Publisher.Domain.Abstractions;

/// <summary>
/// Transactional save boundary for one or more aggregates. Implementation in
/// Infrastructure flushes the EF Core change tracker + dispatches collected
/// domain events through Wolverine's in-process bus.
/// </summary>
public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
