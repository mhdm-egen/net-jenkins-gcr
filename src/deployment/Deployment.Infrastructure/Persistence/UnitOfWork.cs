using Deployment.Domain.Abstractions;
using Deployment.Domain.Common;
using Microsoft.EntityFrameworkCore;

namespace Deployment.Infrastructure.Persistence;

/// <summary>
/// Wraps <see cref="DeploymentDbContext.SaveChangesAsync"/> with the
/// domain-event dispatch step. Events raised on tracked aggregates are
/// collected, the change-tracker is flushed, then events are published via
/// the in-process bus — same transaction boundary, handlers see the persisted
/// state.
/// </summary>
public sealed class UnitOfWork : IUnitOfWork
{
    private readonly DeploymentDbContext _db;
    private readonly IDomainEventDispatcher _events;

    public UnitOfWork(DeploymentDbContext db, IDomainEventDispatcher events)
    {
        _db = db;
        _events = events;
    }

    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        // Snapshot domain events BEFORE save so we don't miss any raised in
        // EF-managed value converters or shadow properties; clear them on the
        // aggregates afterward so subsequent saves don't re-dispatch.
        var pendingEvents = _db.ChangeTracker
            .Entries()
            .Select(e => e.Entity)
            .OfType<AggregateRoot<Guid>>()         // PR-1: only Guid-keyed roots — generalize if/when other key types appear
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
