namespace Cicd.IntegrationEvents.Publisher;

/// <summary>
/// A container image was promoted (pushed) from the local Nexus registry to a remote registry
/// (e.g. Google Artifact Registry) by the publisher service. Emitted on the "publisher.events"
/// channel; downstream services (deployment, notifications) may react to the remote image now
/// being available.
/// </summary>
public sealed record ContainerPromoted(
    Guid EventId,
    DateTimeOffset OccurredAtUtc,
    Guid PromotionId,
    Guid ContainerId,
    Guid RepositoryId,
    string ContainerName,
    string Version,
    string SourceRef,
    string RemoteRef,
    Guid RegistryId,
    string RegistryName) : IIntegrationEvent;
