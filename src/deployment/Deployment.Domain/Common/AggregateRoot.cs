namespace Deployment.Domain.Common;

/// <summary>
/// Root of a consistency boundary. Only aggregate roots are loaded/saved by
/// repositories; everything inside the aggregate is reached through the root.
/// Domain events raised here are collected on save and dispatched by the
/// Infrastructure's UnitOfWork.
/// </summary>
public abstract class AggregateRoot<TId> : Entity<TId>
    where TId : notnull
{
    private readonly List<IDomainEvent> _domainEvents = new();

    /// <summary>Events raised since the aggregate was loaded; cleared after dispatch.</summary>
    public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    protected void RaiseEvent(IDomainEvent @event) => _domainEvents.Add(@event);

    public void ClearDomainEvents() => _domainEvents.Clear();
}
