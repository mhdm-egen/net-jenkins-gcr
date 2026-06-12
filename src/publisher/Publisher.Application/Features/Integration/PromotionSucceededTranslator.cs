using Publisher.Domain.Promotions.Events;

namespace Publisher.Application.Features.Integration;

/// <summary>
/// Producer edge (publisher → bus): translates the internal <see cref="PromotionSucceeded"/>
/// domain event into the cross-service <see cref="Cicd.IntegrationEvents.Publisher.ContainerPromoted"/>
/// integration event. Discovered by Wolverine; the cascaded return is published through the SQL
/// outbox onto the "publisher.events" channel.
/// </summary>
public sealed class PromotionSucceededTranslator
{
    public Cicd.IntegrationEvents.Publisher.ContainerPromoted Handle(PromotionSucceeded evt)
        => new(
            EventId: Guid.NewGuid(),
            OccurredAtUtc: evt.OccurredAtUtc,
            PromotionId: evt.PromotionId,
            ContainerId: evt.ContainerId,
            RepositoryId: evt.RepositoryId,
            ContainerName: evt.ContainerName,
            Version: evt.Version,
            SourceRef: evt.SourceRef,
            RemoteRef: evt.RemoteRef,
            RegistryId: evt.RegistryId,
            RegistryName: evt.RegistryName);
}
