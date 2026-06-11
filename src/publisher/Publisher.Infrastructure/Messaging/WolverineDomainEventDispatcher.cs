using Publisher.Domain.Common;
using Publisher.Infrastructure.Persistence;
using Wolverine;

namespace Publisher.Infrastructure.Messaging;

/// <summary>
/// Adapts the Domain-blind <see cref="IDomainEventDispatcher"/> abstraction to Wolverine's
/// in-process bus. UnitOfWork holds the snapshot of events; this just hands each one to
/// <see cref="IMessageBus.PublishAsync"/>.
/// </summary>
public sealed class WolverineDomainEventDispatcher : IDomainEventDispatcher
{
    private readonly IMessageBus _bus;

    public WolverineDomainEventDispatcher(IMessageBus bus) => _bus = bus;

    public Task DispatchAsync(IDomainEvent domainEvent, CancellationToken cancellationToken = default)
        => _bus.PublishAsync(domainEvent).AsTask();
}
