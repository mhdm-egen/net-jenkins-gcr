namespace Deployment.Contracts.Catalog;

// --- Read-side DTOs ---

public sealed record ContainerImageDto(
    Guid Id,
    string Registry,
    string Repository,
    string Name,
    string DefaultTag,
    string BaseRef,
    bool IsActive,
    DateTimeOffset CreatedAtUtc);

/// <summary>The result of resolving a coordinate + tag to an immutable digest reference.</summary>
public sealed record ContainerImageResolutionDto(
    string Registry,
    string Repository,
    string Name,
    string Tag,
    string Digest,
    string DigestRef);

// --- Write-side requests ---

public sealed record RegisterContainerImageRequest(
    string Registry,
    string Repository,
    string Name,
    string? DefaultTag);

public sealed record ChangeContainerImageDefaultTagRequest(string DefaultTag);

public sealed record SetContainerImageActiveRequest(bool IsActive);
