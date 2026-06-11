namespace Cicd.IntegrationEvents.Ci;

/// <summary>
/// A container image produced by a CI build was published to the registry. Emitted by the
/// Jenkins CI service; downstream services (deployment, publishing, notifications) may react.
/// </summary>
public sealed record ContainerPublished(
    Guid EventId,
    DateTimeOffset OccurredAtUtc,
    Guid BuildId,
    Guid RepositoryId,
    string ContainerName,
    string ArtifactUri,
    string Version,
    string CommitSha) : IIntegrationEvent;
