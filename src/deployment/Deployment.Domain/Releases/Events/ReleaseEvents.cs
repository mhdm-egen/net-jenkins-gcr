using Deployment.Domain.Common;

namespace Deployment.Domain.Releases.Events;

public sealed record ReleasePublished(
    Guid ReleaseId,
    Guid DeployableUnitId,
    string SemanticVersion,
    string BuildNumber,
    string CommitSha,
    ArtifactType ArtifactType,
    string? ArtifactUri,
    DateTimeOffset OccurredAtUtc) : IDomainEvent;

public sealed record ReleaseProvenanceAttached(
    Guid ReleaseId,
    string ArtifactSha256,
    string SbomUri,
    string VulnerabilityReportUri,
    string CiRunUrl,
    string CiRunId,
    string PublishedByPrincipal,
    DateTimeOffset OccurredAtUtc) : IDomainEvent;

/// <summary>
/// Drives the <c>ReleaseStatusChange</c> history projection (decisions §9.2).
/// Reason is required for transitions to <c>Quarantined</c>; optional otherwise.
/// </summary>
public sealed record ReleaseStatusChanged(
    Guid ReleaseId,
    ReleaseStatus FromStatus,
    ReleaseStatus ToStatus,
    string? Reason,
    string ChangedByPrincipal,
    DateTimeOffset OccurredAtUtc) : IDomainEvent;

public sealed record ReleaseCompositionEntryAdded(
    Guid ApplicationReleaseId,
    Guid ServiceId,
    PinMode PinMode,
    Guid? ServiceReleaseId,
    DateTimeOffset OccurredAtUtc) : IDomainEvent;

public sealed record ReleaseCompositionEntryUpdated(
    Guid ApplicationReleaseId,
    Guid ServiceId,
    PinMode PinMode,
    Guid? ServiceReleaseId,
    DateTimeOffset OccurredAtUtc) : IDomainEvent;

public sealed record ReleaseCompositionEntryRemoved(
    Guid ApplicationReleaseId,
    Guid ServiceId,
    DateTimeOffset OccurredAtUtc) : IDomainEvent;
