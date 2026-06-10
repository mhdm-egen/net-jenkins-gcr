namespace Jenkins.Contracts.Builds;

// Wire-stable mirrors of the Jenkins.Domain enums. Integer values must match the
// domain enums one-for-one (see Deployment.Contracts for the established pattern).

public enum BuildStatusDto
{
    Queued = 0,
    Running = 1,
    Succeeded = 2,
    Failed = 3,
    Aborted = 4,
}

public enum ArtifactKindDto
{
    NuGetPackage = 0,
    ContainerImage = 1,
}

public enum PublicationRegistryDto
{
    NexusNuGet = 0,
    NexusDocker = 1,
}

// --- Read-side DTOs ---

public sealed record BuildSummaryDto(
    Guid Id,
    Guid RepositoryId,
    string CiJobName,
    int CiBuildNumber,
    string CiRunUrl,
    string CommitShort,
    string Branch,
    string? PackageVersion,
    BuildStatusDto Status,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset? CompletedAtUtc);

public sealed record BuildArtifactDto(
    Guid Id,
    ArtifactKindDto Kind,
    string Name,
    string Version,
    string Digest,
    long? SizeBytes,
    IReadOnlyList<ArtifactPublicationDto> Publications);

public sealed record ArtifactPublicationDto(
    Guid Id,
    PublicationRegistryDto Registry,
    string Reference,
    IReadOnlyList<string> Tags,
    DateTimeOffset PublishedAtUtc);

public sealed record BuildDetailDto(
    BuildSummaryDto Head,
    string? FileVersion,
    string? AssemblyVersion,
    string? InformationalVersion,
    string? BaseVersion,
    string? SbomUri,
    string? VulnerabilityReportUri,
    string? TriggeredBy,
    long? DurationMs,
    IReadOnlyList<BuildArtifactDto> Artifacts);

// --- Write-side requests ---

public sealed record RecordBuildRequest(
    Guid RepositoryId,
    string CiJobName,
    int CiBuildNumber,
    string CiRunUrl,
    string CiRunId,
    string CommitSha,
    string CommitShort,
    string Branch,
    string? Author,
    string? Message,
    DateTimeOffset? CommittedAtUtc,
    string? TriggeredBy,
    DateTimeOffset StartedAtUtc);

public sealed record CompleteBuildRequest(
    BuildStatusDto Status,
    DateTimeOffset CompletedAtUtc,
    long? DurationMs,
    BuildVersionsPayload? Versions,
    BuildQualityPayload? Quality);

public sealed record BuildVersionsPayload(
    string PackageVersion,
    string FileVersion,
    string AssemblyVersion,
    string InformationalVersion,
    string BaseVersion);

public sealed record BuildQualityPayload(
    string SbomUri,
    string VulnerabilityReportUri);

public sealed record RecordArtifactRequest(
    ArtifactKindDto Kind,
    string Name,
    string Version,
    string Digest,
    long? SizeBytes,
    PublicationRegistryDto? Registry,
    string? Reference,
    IReadOnlyList<string>? Tags);
