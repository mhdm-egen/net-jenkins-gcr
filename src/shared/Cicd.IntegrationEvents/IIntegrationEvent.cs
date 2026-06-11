namespace Cicd.IntegrationEvents;

/// <summary>
/// Marker for cross-service integration events carried on the event bus. These are an
/// explicit, versioned wire contract — distinct from each service's internal domain
/// events — so services stay decoupled from one another's domain models.
///
/// Every event carries an <see cref="EventId"/> (for consumer-side idempotency/dedupe)
/// and the <see cref="OccurredAtUtc"/> the originating fact happened.
/// </summary>
public interface IIntegrationEvent
{
    Guid EventId { get; }
    DateTimeOffset OccurredAtUtc { get; }
}
