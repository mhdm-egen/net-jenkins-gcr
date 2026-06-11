namespace Deployment.Contracts.Releases;

// Wire-stable mirrors of the Releases enums. Integer values must match
// Deployment.Domain enums one-for-one (see ServiceKindDto for the pattern).

public enum ReleaseStatusDto
{
    Available = 0,
    Superseded = 1,
    Quarantined = 2,
}

public enum ArtifactTypeDto
{
    Manifest = 0,
    Zip = 1,
    ContainerImage = 2,
    NuGet = 3,
}

public enum PinModeDto
{
    Pinned = 0,
    Latest = 1,
    Current = 2,
}

// --- Read-side DTOs ---

public sealed record ReleaseDto(
    Guid Id,
    Guid DeployableUnitId,
    string DeployableUnitName,
    string SemanticVersion,
    string BuildNumber,
    string CommitSha,
    ArtifactTypeDto ArtifactType,
    string? ArtifactUri,
    ReleaseStatusDto Status,
    DateTimeOffset CreatedAtUtc,
    ProvenanceDto? Provenance,
    IReadOnlyList<ReleaseCompositionEntryDto> Compositions);

public sealed record ProvenanceDto(
    string ArtifactSha256,
    string SbomUri,
    string VulnerabilityReportUri,
    string CiRunUrl,
    string CiRunId,
    string PublishedByPrincipal);

public sealed record ReleaseCompositionEntryDto(
    Guid ServiceId,
    string ServiceName,
    PinModeDto PinMode,
    Guid? ServiceReleaseId,
    string? ServiceReleaseVersion);

public sealed record ReleaseStatusChangeDto(
    Guid ChangeId,
    Guid ReleaseId,
    ReleaseStatusDto FromStatus,
    ReleaseStatusDto ToStatus,
    string? Reason,
    string ChangedByPrincipal,
    DateTimeOffset ChangedAtUtc);

// --- Write-side requests ---

public sealed record PublishReleaseRequest(
    Guid DeployableUnitId,
    string SemanticVersion,
    string BuildNumber,
    string CommitSha,
    ArtifactTypeDto ArtifactType,
    string? ArtifactUri);

public sealed record AttachProvenanceRequest(
    string ArtifactSha256,
    string SbomUri,
    string VulnerabilityReportUri,
    string CiRunUrl,
    string CiRunId,
    string PublishedByPrincipal);

public sealed record ChangeReleaseStatusRequest(
    ReleaseStatusDto NewStatus,
    string? Reason,
    string ChangedByPrincipal);

public sealed record AddCompositionEntryRequest(
    Guid ServiceId,
    PinModeDto PinMode,
    Guid? ServiceReleaseId);

public sealed record UpdateCompositionEntryRequest(
    PinModeDto PinMode,
    Guid? ServiceReleaseId);
