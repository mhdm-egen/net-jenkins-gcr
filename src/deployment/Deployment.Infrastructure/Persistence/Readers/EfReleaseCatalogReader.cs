using Deployment.Application.Features.Releases.ListReleases;
using Deployment.Contracts.Releases;
using Microsoft.EntityFrameworkCore;

namespace Deployment.Infrastructure.Persistence.Readers;

/// <summary>
/// EF projection for the read-side of the Releases UI. Hand-shaped queries
/// rather than aggregate hydration so list pages stay cheap.
/// </summary>
internal sealed class EfReleaseCatalogReader : IReleaseCatalogReader
{
    private readonly DeploymentDbContext _db;

    public EfReleaseCatalogReader(DeploymentDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<ReleaseDto>> ListByUnitAsync(
        Guid deployableUnitId, CancellationToken cancellationToken = default)
    {
        var rows = await (
            from r in _db.Releases.AsNoTracking()
            join u in _db.DeployableUnits.AsNoTracking() on r.DeployableUnitId equals u.Id
            where r.DeployableUnitId == deployableUnitId
            orderby r.CreatedAtUtc descending
            select new
            {
                r.Id, r.DeployableUnitId, UnitName = u.Name,
                r.SemanticVersion, r.BuildNumber, r.CommitSha,
                r.ArtifactType, r.ArtifactUri, r.Status, r.CreatedAtUtc,
                r.Provenance,
            }
        ).ToListAsync(cancellationToken).ConfigureAwait(false);

        // The list view doesn't need compositions hydrated; mappers receive empty lookups.
        var empty = (IReadOnlyDictionary<Guid, string>)new Dictionary<Guid, string>();

        return rows.Select(x => new ReleaseDto(
            Id: x.Id,
            DeployableUnitId: x.DeployableUnitId,
            DeployableUnitName: x.UnitName,
            SemanticVersion: x.SemanticVersion,
            BuildNumber: x.BuildNumber,
            CommitSha: x.CommitSha,
            ArtifactType: (ArtifactTypeDto)(int)x.ArtifactType,
            ArtifactUri: x.ArtifactUri,
            Status: (ReleaseStatusDto)(int)x.Status,
            CreatedAtUtc: x.CreatedAtUtc,
            Provenance: x.Provenance is null ? null : new ProvenanceDto(
                x.Provenance.ArtifactSha256, x.Provenance.SbomUri,
                x.Provenance.VulnerabilityReportUri, x.Provenance.CiRunUrl,
                x.Provenance.CiRunId, x.Provenance.PublishedByPrincipal),
            Compositions: Array.Empty<ReleaseCompositionEntryDto>()))
            .ToList();
    }

    public async Task<ReleaseDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var row = await (
            from r in _db.Releases.AsNoTracking()
            join u in _db.DeployableUnits.AsNoTracking() on r.DeployableUnitId equals u.Id
            where r.Id == id
            select new
            {
                r.Id, r.DeployableUnitId, UnitName = u.Name,
                r.SemanticVersion, r.BuildNumber, r.CommitSha,
                r.ArtifactType, r.ArtifactUri, r.Status, r.CreatedAtUtc,
                r.Provenance,
            }
        ).FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);

        if (row is null) return null;

        // Compositions: pull the BOM with denormalized service names and (where present) the pinned release version.
        var comps = await (
            from c in _db.ReleaseCompositions.AsNoTracking()
            join u in _db.DeployableUnits.AsNoTracking() on c.ServiceId equals u.Id
            join pinned in _db.Releases.AsNoTracking()
                on c.ServiceReleaseId equals pinned.Id into pinnedJoin
            from pin in pinnedJoin.DefaultIfEmpty()
            where c.ApplicationReleaseId == id
            select new ReleaseCompositionEntryDto(
                c.ServiceId,
                u.Name,
                (PinModeDto)(int)c.PinMode,
                c.ServiceReleaseId,
                pin == null ? null : pin.SemanticVersion)
        ).ToListAsync(cancellationToken).ConfigureAwait(false);

        return new ReleaseDto(
            Id: row.Id,
            DeployableUnitId: row.DeployableUnitId,
            DeployableUnitName: row.UnitName,
            SemanticVersion: row.SemanticVersion,
            BuildNumber: row.BuildNumber,
            CommitSha: row.CommitSha,
            ArtifactType: (ArtifactTypeDto)(int)row.ArtifactType,
            ArtifactUri: row.ArtifactUri,
            Status: (ReleaseStatusDto)(int)row.Status,
            CreatedAtUtc: row.CreatedAtUtc,
            Provenance: row.Provenance is null ? null : new ProvenanceDto(
                row.Provenance.ArtifactSha256, row.Provenance.SbomUri,
                row.Provenance.VulnerabilityReportUri, row.Provenance.CiRunUrl,
                row.Provenance.CiRunId, row.Provenance.PublishedByPrincipal),
            Compositions: comps);
    }

    public async Task<IReadOnlyList<ReleaseStatusChangeDto>> GetStatusHistoryAsync(
        Guid releaseId, CancellationToken cancellationToken = default)
    {
        var rows = await _db.ReleaseStatusChanges.AsNoTracking()
            .Where(c => c.ReleaseId == releaseId)
            .OrderByDescending(c => c.ChangedAtUtc)
            .Select(c => new ReleaseStatusChangeDto(
                c.ChangeId,
                c.ReleaseId,
                (ReleaseStatusDto)(int)c.FromStatus,
                (ReleaseStatusDto)(int)c.ToStatus,
                c.Reason,
                c.ChangedByPrincipal,
                c.ChangedAtUtc))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        return rows;
    }
}
