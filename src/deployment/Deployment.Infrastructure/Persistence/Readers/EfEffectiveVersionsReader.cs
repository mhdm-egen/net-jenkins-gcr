using Deployment.Application.Features.Deployments.GetEffectiveVersions;
using Deployment.Domain.Deployments;
using Microsoft.EntityFrameworkCore;

namespace Deployment.Infrastructure.Persistence.Readers;

/// <summary>
/// EF implementation of Q1′ — effective running version per (service, target)
/// in an environment (decisions §10.4). The hot-path index
/// <c>Deployment(TargetId, Status, CompletedAtUtc)</c> backs the inner
/// per-target lookup.
/// </summary>
internal sealed class EfEffectiveVersionsReader : IEffectiveVersionsReader
{
    private readonly DeploymentDbContext _db;

    public EfEffectiveVersionsReader(DeploymentDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<EffectiveVersionEntry>> ReadAsync(
        Guid applicationId,
        Guid environmentId,
        CancellationToken cancellationToken = default)
    {
        // Service ids + names that compose the app (catalog membership).
        var members = await _db.ApplicationServices
            .Where(aps => aps.ApplicationId == applicationId)
            .Join(_db.DeployableUnits, aps => aps.ServiceId, u => u.Id,
                (aps, u) => new { aps.ServiceId, ServiceName = u.Name })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var targets = await _db.DeploymentTargets
            .Where(t => t.EnvironmentId == environmentId)
            .Select(t => new { t.Id, t.ResourceId, t.Region })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if (members.Count == 0 || targets.Count == 0)
            return Array.Empty<EffectiveVersionEntry>();

        var serviceIds = members.Select(m => m.ServiceId).ToList();
        var targetIds = targets.Select(t => t.Id).ToList();

        // Per (service, target), the most recent succeeded deployment.
        var latestByPair = await (
            from d in _db.Deployments
            join r in _db.Releases on d.ReleaseId equals r.Id
            where d.Status == DeploymentStatus.Succeeded
                  && d.TargetId.HasValue
                  && targetIds.Contains(d.TargetId.Value)
                  && serviceIds.Contains(r.DeployableUnitId)
            group new { d, r } by new { r.DeployableUnitId, d.TargetId } into g
            select new
            {
                g.Key.DeployableUnitId,
                TargetId = g.Key.TargetId!.Value,
                Latest = g.OrderByDescending(x => x.d.CompletedAtUtc).First(),
            }
        ).ToListAsync(cancellationToken).ConfigureAwait(false);

        var byPair = latestByPair.ToDictionary(x => (x.DeployableUnitId, x.TargetId));

        var entries = new List<EffectiveVersionEntry>(members.Count * targets.Count);
        foreach (var m in members)
        {
            foreach (var t in targets)
            {
                byPair.TryGetValue((m.ServiceId, t.Id), out var hit);
                entries.Add(new EffectiveVersionEntry(
                    ServiceId: m.ServiceId,
                    ServiceName: m.ServiceName,
                    TargetId: t.Id,
                    TargetResourceId: t.ResourceId,
                    Region: t.Region,
                    RunningReleaseId: hit?.Latest.d.ReleaseId,
                    SemanticVersion: hit?.Latest.r.SemanticVersion,
                    CompletedAtUtc: hit?.Latest.d.CompletedAtUtc));
            }
        }
        return entries;
    }
}
