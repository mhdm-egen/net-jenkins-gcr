namespace Jenkins.Contracts.Handoffs;

public enum HandoffStatusDto
{
    Pending = 0,
    Published = 1,
    Failed = 2,
    Skipped = 3,
}

// --- Read-side DTO ---

public sealed record ContainerReleaseHandoffDto(
    Guid Id,
    Guid BuildId,
    Guid BuildArtifactId,
    Guid DeployableComponentId,
    Guid RepositoryId,
    Guid DeployableUnitId,
    Guid? DeploymentReleaseId,
    string SemanticVersion,
    string ArtifactUri,
    HandoffStatusDto Status,
    string RequestedByPrincipal,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? SettledAtUtc,
    string? FailureReason);

// --- Write-side request ---

/// <summary>Promote a green container build to a deployment Release (manual gate, decision #3).</summary>
public sealed record PromoteBuildRequest(
    Guid BuildId,
    Guid BuildArtifactId,
    string RequestedByPrincipal);
