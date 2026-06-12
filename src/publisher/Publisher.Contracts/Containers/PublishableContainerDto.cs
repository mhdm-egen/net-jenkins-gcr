namespace Publisher.Contracts.Containers;

public enum ContainerSourceDto
{
    Bus = 0,
    Manual = 1,
}

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
    bool IsActive,
    ContainerSourceDto Source,
    DateTimeOffset FirstSeenAtUtc,
    DateTimeOffset LastSeenAtUtc);

/// <summary>
/// Body for manually adding a container to the inventory (picked from the local Nexus docker
/// registry in the UI). The record is created active. <see cref="ArtifactUri"/> is the full Nexus
/// pull reference (host/name:tag); <see cref="CommitSha"/> is optional (parsed from the tag).
/// </summary>
public sealed record AddContainerRequest(
    string ContainerName,
    string Version,
    string? CommitSha,
    string ArtifactUri);
