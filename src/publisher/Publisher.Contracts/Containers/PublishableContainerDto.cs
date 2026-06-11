namespace Publisher.Contracts.Containers;

/// <summary>
/// Wire shape of a container in the publisher's inventory (sourced from local Nexus).
/// </summary>
public sealed record PublishableContainerDto(
    Guid Id,
    Guid RepositoryId,
    Guid BuildId,
    string ContainerName,
    string Version,
    string CommitSha,
    string ArtifactUri,
    string? ImageDigest,
    DateTimeOffset FirstSeenAtUtc,
    DateTimeOffset LastSeenAtUtc);
