using Microsoft.EntityFrameworkCore;
using Publisher.Domain.Abstractions;
using Publisher.Domain.Common;

namespace Publisher.Infrastructure.Persistence;

/// <summary>
/// Wraps <see cref="PublisherDbContext.SaveChangesAsync"/> with the domain-event dispatch step.
/// Events raised on tracked aggregates are snapshotted before the flush, persisted with the
/// change-tracker, then published via the in-process bus — same transaction boundary, handlers
/// see the persisted state.
/// </summary>
public sealed class UnitOfWork : IUnitOfWork
{
    private readonly PublisherDbContext _db;
    private readonly IDomainEventDispatcher _events;

    public UnitOfWork(PublisherDbContext db, IDomainEventDispatcher events)
    {
        _db = db;
        _events = events;
    }

    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var pendingEvents = _db.ChangeTracker
            .Entries()
            .Select(e => e.Entity)
            .OfType<AggregateRoot<Guid>>()
            .SelectMany(a =>
            {
                var events = a.DomainEvents.ToArray();
                a.ClearDomainEvents();
                return events;
            })
            .ToArray();

        var written = await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        foreach (var evt in pendingEvents)
        {
            await _events.DispatchAsync(evt, cancellationToken).ConfigureAwait(false);
        }
        return written;
    }
}

/// <summary>
/// Abstraction over whatever bus we end up using (Wolverine in production).
/// Kept Infrastructure-internal — Domain doesn't know it exists.
/// </summary>
public interface IDomainEventDispatcher
{
    Task DispatchAsync(IDomainEvent domainEvent, CancellationToken cancellationToken = default);
}
