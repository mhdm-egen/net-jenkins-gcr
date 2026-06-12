using Wolverine.Attributes;

namespace Jenkins.Application.Features.Integration;

/// <summary>
/// Translation edge (CI → bus): when the internal <see cref="Jenkins.Domain.Builds.Events.ContainerPublished"/>
/// domain event fires, map it to the cross-service <see cref="Cicd.IntegrationEvents.Ci.ContainerPublished"/>
/// integration event. Returning the event cascades it through Wolverine onto the bus (routed to the
/// "ci.events" channel by the topology, persisted via the outbox). Pure, synchronous, dependency-free —
/// the domain event already carries everything — so Wolverine inlines it cleanly (same shape as the
/// pipeline-run translators).
///
/// [WolverineHandler] is REQUIRED: Wolverine's convention only auto-discovers types whose names end in
/// "Handler"/"Consumer", so a "*Translator" is invisible without it (and the integration event is never
/// published).
/// </summary>
[WolverineHandler]
public sealed class ContainerPublishedTranslator
{
    public Cicd.IntegrationEvents.Ci.ContainerPublished Handle(Jenkins.Domain.Builds.Events.ContainerPublished evt)
        => new(
            EventId: Guid.NewGuid(),
            OccurredAtUtc: evt.OccurredAtUtc,
            BuildId: evt.BuildId,
            RepositoryId: evt.RepositoryId,
            ContainerName: evt.ContainerName,
            ArtifactUri: evt.Reference,
            Version: evt.Version,
            CommitSha: evt.CommitSha);
}
