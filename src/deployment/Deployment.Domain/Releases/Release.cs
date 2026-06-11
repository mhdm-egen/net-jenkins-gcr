using Deployment.Domain.Common;
using Deployment.Domain.Releases.Events;

namespace Deployment.Domain.Releases;

/// <summary>
/// Immutable, environment-neutral catalog entry for a published version of a
/// <c>DeployableUnit</c> (Service or Application). "Build once, deploy many."
///
/// Two flavors share this single type, distinguished by the unit they reference:
/// - Service release: real artifact, <see cref="ArtifactUri"/> set, no compositions.
/// - Application release: manifest-only (<see cref="ArtifactType.Manifest"/>),
///   <see cref="ArtifactUri"/> null, BOM lives in <see cref="Compositions"/>.
///
/// Invariants:
/// - Unique on (DeployableUnitId, SemanticVersion) — enforced at DB layer.
/// - <see cref="ArtifactType.Manifest"/> implies ArtifactUri is null; any other
///   artifact type requires ArtifactUri.
/// - Compositions are only meaningful on Application releases. Service releases
///   should not have compositions.
/// </summary>
public sealed class Release : AggregateRoot<Guid>
{
    public Guid DeployableUnitId { get; private set; }
    public string SemanticVersion { get; private set; }
    public string BuildNumber { get; private set; }
    public string CommitSha { get; private set; }
    public ArtifactType ArtifactType { get; private set; }
    public string? ArtifactUri { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public ReleaseStatus Status { get; private set; }

    /// <summary>Supply-chain provenance; null on releases predating publish-pipeline support.</summary>
    public Provenance? Provenance { get; private set; }

    private readonly List<ReleaseComposition> _compositions = new();
    public IReadOnlyCollection<ReleaseComposition> Compositions => _compositions.AsReadOnly();

    private Release()
    {
        SemanticVersion = string.Empty;
        BuildNumber = string.Empty;
        CommitSha = string.Empty;
    }

    public Release(
        Guid id,
        Guid deployableUnitId,
        string semanticVersion,
        string buildNumber,
        string commitSha,
        ArtifactType artifactType,
        string? artifactUri,
        DateTimeOffset createdAtUtc)
    {
        if (id == Guid.Empty) throw new ArgumentException("Id cannot be empty.", nameof(id));
        if (deployableUnitId == Guid.Empty)
            throw new ArgumentException("DeployableUnitId cannot be empty.", nameof(deployableUnitId));
        if (string.IsNullOrWhiteSpace(semanticVersion))
            throw new ArgumentException("SemanticVersion cannot be empty.", nameof(semanticVersion));
        if (string.IsNullOrWhiteSpace(buildNumber))
            throw new ArgumentException("BuildNumber cannot be empty.", nameof(buildNumber));
        if (string.IsNullOrWhiteSpace(commitSha))
            throw new ArgumentException("CommitSha cannot be empty.", nameof(commitSha));

        ValidateArtifactInvariant(artifactType, artifactUri);

        Id = id;
        DeployableUnitId = deployableUnitId;
        SemanticVersion = semanticVersion.Trim();
        BuildNumber = buildNumber.Trim();
        CommitSha = commitSha.Trim();
        ArtifactType = artifactType;
        ArtifactUri = artifactUri?.Trim();
        CreatedAtUtc = createdAtUtc;
        Status = ReleaseStatus.Available;

        RaiseEvent(new ReleasePublished(
            id, deployableUnitId, SemanticVersion, BuildNumber, CommitSha,
            artifactType, ArtifactUri, createdAtUtc));
    }

    /// <summary>
    /// Attach supply-chain provenance (decisions §9.1). Setting overwrites any
    /// previously attached value — the publish pipeline owns this field.
    /// </summary>
    public void AttachProvenance(Provenance provenance, DateTimeOffset occurredAtUtc)
    {
        ArgumentNullException.ThrowIfNull(provenance);
        if (provenance.Equals(Provenance)) return;
        Provenance = provenance;
        RaiseEvent(new ReleaseProvenanceAttached(
            Id,
            provenance.ArtifactSha256,
            provenance.SbomUri,
            provenance.VulnerabilityReportUri,
            provenance.CiRunUrl,
            provenance.CiRunId,
            provenance.PublishedByPrincipal,
            occurredAtUtc));
    }

    /// <summary>
    /// Transition the release status with audit. Reason is required when moving
    /// to <see cref="ReleaseStatus.Quarantined"/> (you must say why); optional
    /// otherwise. The <c>ReleaseStatusChange</c> projection captures the full
    /// timeline (§9.2).
    /// </summary>
    public void ChangeStatus(
        ReleaseStatus newStatus,
        string? reason,
        string changedByPrincipal,
        DateTimeOffset occurredAtUtc)
    {
        if (string.IsNullOrWhiteSpace(changedByPrincipal))
            throw new ArgumentException("ChangedByPrincipal cannot be empty.", nameof(changedByPrincipal));
        if (newStatus == ReleaseStatus.Quarantined && string.IsNullOrWhiteSpace(reason))
            throw new InvalidOperationException(
                "A reason is required when moving a release to Quarantined.");

        if (Status == newStatus) return;
        var oldStatus = Status;
        Status = newStatus;
        RaiseEvent(new ReleaseStatusChanged(Id, oldStatus, newStatus, reason?.Trim(),
            changedByPrincipal.Trim(), occurredAtUtc));
    }

    // --- Composition (BOM) management — only meaningful on Application releases ---

    /// <summary>
    /// Add a BOM entry. Idempotency: adding an entry for an existing
    /// <paramref name="serviceId"/> throws — use <see cref="UpdateComposition"/>
    /// to change pin mode / version.
    /// </summary>
    public void AddComposition(
        Guid serviceId,
        PinMode pinMode,
        Guid? serviceReleaseId,
        DateTimeOffset occurredAtUtc)
    {
        if (ArtifactType != ArtifactType.Manifest)
            throw new InvalidOperationException(
                "Compositions are only valid on Application releases (ArtifactType = Manifest).");
        if (_compositions.Any(c => c.ServiceId == serviceId))
            throw new InvalidOperationException(
                $"Composition for service {serviceId} already exists on release {Id}.");

        var entry = new ReleaseComposition(Id, serviceId, pinMode, serviceReleaseId);
        _compositions.Add(entry);
        RaiseEvent(new ReleaseCompositionEntryAdded(Id, serviceId, pinMode, serviceReleaseId, occurredAtUtc));
    }

    public void UpdateComposition(
        Guid serviceId,
        PinMode pinMode,
        Guid? serviceReleaseId,
        DateTimeOffset occurredAtUtc)
    {
        var entry = _compositions.FirstOrDefault(c => c.ServiceId == serviceId)
            ?? throw new InvalidOperationException(
                $"Composition for service {serviceId} not found on release {Id}.");

        entry.Update(pinMode, serviceReleaseId);
        RaiseEvent(new ReleaseCompositionEntryUpdated(Id, serviceId, pinMode, serviceReleaseId, occurredAtUtc));
    }

    public void RemoveComposition(Guid serviceId, DateTimeOffset occurredAtUtc)
    {
        var entry = _compositions.FirstOrDefault(c => c.ServiceId == serviceId);
        if (entry is null) return;
        _compositions.Remove(entry);
        RaiseEvent(new ReleaseCompositionEntryRemoved(Id, serviceId, occurredAtUtc));
    }

    private static void ValidateArtifactInvariant(ArtifactType artifactType, string? artifactUri)
    {
        if (artifactType == ArtifactType.Manifest)
        {
            if (!string.IsNullOrWhiteSpace(artifactUri))
                throw new InvalidOperationException(
                    "ArtifactUri must be null for Manifest releases (Application releases are BOM-only).");
        }
        else
        {
            if (string.IsNullOrWhiteSpace(artifactUri))
                throw new InvalidOperationException(
                    $"ArtifactUri is required for ArtifactType={artifactType}.");
        }
    }
}
