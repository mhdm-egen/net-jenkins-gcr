using Deployment.Application.Features.Environments.ListEnvironments;
using Deployment.Contracts.Environments;
using Microsoft.EntityFrameworkCore;

namespace Deployment.Infrastructure.Persistence.Readers;

internal sealed class EfEnvironmentCatalogReader : IEnvironmentCatalogReader
{
    private readonly DeploymentDbContext _db;

    public EfEnvironmentCatalogReader(DeploymentDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<EnvironmentDto>> ListAsync(CancellationToken cancellationToken = default)
    {
        // Heads + targets + freeze windows in three round-trips, then stitched
        // in memory — keeps each query simple and avoids cartesian fan-out.
        var heads = await _db.Environments.AsNoTracking()
            .OrderBy(e => e.PromotionRank).ThenBy(e => e.Name)
            .Select(e => new
            {
                e.Id, e.Name, e.PromotionRank, e.RequiresApproval, e.IsProduction,
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if (heads.Count == 0) return Array.Empty<EnvironmentDto>();

        var envIds = heads.Select(h => h.Id).ToList();

        var targets = await _db.DeploymentTargets.AsNoTracking()
            .Where(t => envIds.Contains(t.EnvironmentId))
            .OrderBy(t => t.Region).ThenBy(t => t.ResourceId)
            .Select(t => new
            {
                t.Id, t.EnvironmentId, t.TargetKind, t.ResourceId, t.Region, t.Slot,
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var windows = await _db.EnvironmentFreezeWindows.AsNoTracking()
            .Where(w => envIds.Contains(w.EnvironmentId))
            .OrderByDescending(w => w.StartUtc)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var targetsByEnv = targets
            .GroupBy(t => t.EnvironmentId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var windowsByEnv = windows
            .GroupBy(w => w.EnvironmentId)
            .ToDictionary(g => g.Key, g => g.ToList());

        return heads.Select(h => new EnvironmentDto(
            Id: h.Id,
            Name: h.Name,
            PromotionRank: h.PromotionRank,
            RequiresApproval: h.RequiresApproval,
            IsProduction: h.IsProduction,
            Targets: targetsByEnv.TryGetValue(h.Id, out var ts)
                ? ts.Select(t => new DeploymentTargetDto(
                    t.Id, t.EnvironmentId,
                    (TargetKindDto)(int)t.TargetKind,
                    t.ResourceId, t.Region, t.Slot)).ToList()
                : Array.Empty<DeploymentTargetDto>(),
            FreezeWindows: windowsByEnv.TryGetValue(h.Id, out var ws)
                ? ws.Select(w => new EnvironmentFreezeWindowDto(
                    w.Id, w.EnvironmentId, w.StartUtc, w.EndUtc,
                    w.Reason, w.CreatedByPrincipal, w.CreatedAtUtc)).ToList()
                : Array.Empty<EnvironmentFreezeWindowDto>()))
            .ToList();
    }

    public async Task<EnvironmentDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var head = await _db.Environments.AsNoTracking()
            .Where(e => e.Id == id)
            .Select(e => new
            {
                e.Id, e.Name, e.PromotionRank, e.RequiresApproval, e.IsProduction,
            })
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        if (head is null) return null;

        var targets = await _db.DeploymentTargets.AsNoTracking()
            .Where(t => t.EnvironmentId == id)
            .OrderBy(t => t.Region).ThenBy(t => t.ResourceId)
            .Select(t => new DeploymentTargetDto(
                t.Id, t.EnvironmentId,
                (TargetKindDto)(int)t.TargetKind,
                t.ResourceId, t.Region, t.Slot))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var windows = await _db.EnvironmentFreezeWindows.AsNoTracking()
            .Where(w => w.EnvironmentId == id)
            .OrderByDescending(w => w.StartUtc)
            .Select(w => new EnvironmentFreezeWindowDto(
                w.Id, w.EnvironmentId, w.StartUtc, w.EndUtc,
                w.Reason, w.CreatedByPrincipal, w.CreatedAtUtc))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return new EnvironmentDto(
            Id: head.Id,
            Name: head.Name,
            PromotionRank: head.PromotionRank,
            RequiresApproval: head.RequiresApproval,
            IsProduction: head.IsProduction,
            Targets: targets,
            FreezeWindows: windows);
    }
}
