using Jenkins.Domain.Abstractions;
using Jenkins.Domain.Common;
using Microsoft.EntityFrameworkCore;

namespace Jenkins.Infrastructure.Persistence;

/// <summary>
/// Wraps <see cref="JenkinsCiDbContext.SaveChangesAsync"/> with domain-event
/// dispatch. Events raised on tracked aggregates are snapshotted, the change
/// tracker is flushed, then events are published via the in-process bus.
/// </summary>
public sealed class UnitOfWork : IUnitOfWork
{
    private readonly JenkinsCiDbContext _db;
    private readonly IDomainEventDispatcher _events;

    public UnitOfWork(JenkinsCiDbContext db, IDomainEventDispatcher events)
    {
        _db = db;
        _events = events;
    }

    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        // Snapshot + clear events BEFORE save so a later save doesn't re-dispatch.
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
/// Abstraction over the bus (Wolverine in production). Infrastructure-internal —
/// the Domain doesn't know it exists.
/// </summary>
public interface IDomainEventDispatcher
{
    Task DispatchAsync(IDomainEvent domainEvent, CancellationToken cancellationToken = default);
}
