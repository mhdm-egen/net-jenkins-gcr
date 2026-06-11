using Deployment.Contracts.Releases;
using Deployment.Domain.Releases;

namespace Deployment.Application.Features.Releases;

/// <summary>
/// Single place that projects between domain <see cref="Release"/> and the
/// wire-format <see cref="ReleaseDto"/>. The mapper takes lookup dictionaries
/// for unit/service names so EF-side queries can hydrate them in one shot
/// rather than fanning out a query per row.
/// </summary>
internal static class ReleaseMapping
{
    public static ReleaseDto ToDto(
        this Release r,
        string deployableUnitName,
        IReadOnlyDictionary<Guid, string> serviceNames,
        IReadOnlyDictionary<Guid, string> serviceReleaseVersions)
    {
        var compositions = r.Compositions
            .Select(c => new ReleaseCompositionEntryDto(
                ServiceId: c.ServiceId,
                ServiceName: serviceNames.TryGetValue(c.ServiceId, out var sn) ? sn : "(unknown)",
                PinMode: (PinModeDto)(int)c.PinMode,
                ServiceReleaseId: c.ServiceReleaseId,
                ServiceReleaseVersion: c.ServiceReleaseId is { } sid
                    && serviceReleaseVersions.TryGetValue(sid, out var sv) ? sv : null))
            .ToList();

        var provenance = r.Provenance is null ? null : new ProvenanceDto(
            r.Provenance.ArtifactSha256,
            r.Provenance.SbomUri,
            r.Provenance.VulnerabilityReportUri,
            r.Provenance.CiRunUrl,
            r.Provenance.CiRunId,
            r.Provenance.PublishedByPrincipal);

        return new ReleaseDto(
            Id: r.Id,
            DeployableUnitId: r.DeployableUnitId,
            DeployableUnitName: deployableUnitName,
            SemanticVersion: r.SemanticVersion,
            BuildNumber: r.BuildNumber,
            CommitSha: r.CommitSha,
            ArtifactType: (ArtifactTypeDto)(int)r.ArtifactType,
            ArtifactUri: r.ArtifactUri,
            Status: (ReleaseStatusDto)(int)r.Status,
            CreatedAtUtc: r.CreatedAtUtc,
            Provenance: provenance,
            Compositions: compositions);
    }

    public static ArtifactType ToDomain(this ArtifactTypeDto t) => (ArtifactType)(int)t;
    public static ReleaseStatus ToDomain(this ReleaseStatusDto s) => (ReleaseStatus)(int)s;
    public static PinMode ToDomain(this PinModeDto p) => (PinMode)(int)p;
}
