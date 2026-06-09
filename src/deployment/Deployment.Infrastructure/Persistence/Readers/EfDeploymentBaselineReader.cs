using Deployment.Application.Features.Deployments.GetDeploymentBaseline;
using Microsoft.EntityFrameworkCore;

namespace Deployment.Infrastructure.Persistence.Readers;

internal sealed class EfDeploymentBaselineReader : IDeploymentBaselineReader
{
    private readonly DeploymentDbContext _db;

    public EfDeploymentBaselineReader(DeploymentDbContext db)
    {
        _db = db;
    }

    public async Task<DeploymentBaseline?> ReadAsync(Guid deploymentId, CancellationToken cancellationToken = default)
    {
        var head = await _db.Deployments
            .Where(d => d.Id == deploymentId)
            .Select(d => new
            {
                d.Id, d.ReleaseId, d.EnvironmentId, d.Status, d.Strategy, d.Trigger,
                d.TriggeredByPrincipal, d.StartedAtUtc, d.CompletedAtUtc,
                d.SkipPromotionPathReason, d.OverrideFreezeReason,
                d.ParentDeploymentId, d.TargetId,
            })
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        if (head is null) return null;

        var release = await _db.Releases
            .Where(r => r.Id == head.ReleaseId)
            .Select(r => new { r.SemanticVersion })
            .FirstAsync(cancellationToken)
            .ConfigureAwait(false);

        // Cascade siblings: rows whose ParentDeploymentId equals the head id.
        // For non-cascade rows (TargetId not null) the leaves collection is just
        // the head itself.
        var leafQuery =
            from d in _db.Deployments
            join r in _db.Releases on d.ReleaseId equals r.Id
            where d.ParentDeploymentId == head.Id || d.Id == head.Id
            select new
            {
                d.Id, d.ReleaseId, r.SemanticVersion, d.TargetId, d.Status,
            };

        var leafRows = await leafQuery.ToListAsync(cancellationToken).ConfigureAwait(false);

        // For each leaf, attach its secret bindings and target resourceId.
        var leafIds = leafRows.Select(l => l.Id).ToList();
        var targetIds = leafRows.Where(l => l.TargetId.HasValue).Select(l => l.TargetId!.Value).Distinct().ToList();

        var targetMap = await _db.DeploymentTargets
            .Where(t => targetIds.Contains(t.Id))
            .ToDictionaryAsync(t => t.Id, t => t.ResourceId, cancellationToken)
            .ConfigureAwait(false);

        var bindingsByDeployment = await (
            from sb in _db.DeploymentSecretBindings
            join s in _db.ConfigurationSettings on sb.ConfigurationSettingId equals s.Id
            where leafIds.Contains(sb.DeploymentId)
            select new
            {
                sb.DeploymentId,
                Binding = new BaselineSecretBinding(s.Id, s.Key, sb.ResolvedSecretUri),
            }
        ).ToListAsync(cancellationToken).ConfigureAwait(false);

        var bindingMap = bindingsByDeployment
            .GroupBy(x => x.DeploymentId)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<BaselineSecretBinding>)g.Select(x => x.Binding).ToList());

        var leaves = leafRows
            .Where(l => l.Id != head.Id || head.ParentDeploymentId is not null || head.TargetId is not null)
            .Select(l => new BaselineLeaf(
                DeploymentId: l.Id,
                ReleaseId: l.ReleaseId,
                ReleaseSemanticVersion: l.SemanticVersion,
                TargetId: l.TargetId,
                TargetResourceId: l.TargetId is { } tid && targetMap.TryGetValue(tid, out var rid) ? rid : null,
                Status: l.Status,
                SecretBindings: bindingMap.TryGetValue(l.Id, out var sb) ? sb : Array.Empty<BaselineSecretBinding>()))
            .ToList();

        return new DeploymentBaseline(
            DeploymentId: head.Id,
            ReleaseId: head.ReleaseId,
            ReleaseSemanticVersion: release.SemanticVersion,
            EnvironmentId: head.EnvironmentId,
            Status: head.Status,
            Strategy: head.Strategy,
            Trigger: head.Trigger,
            TriggeredByPrincipal: head.TriggeredByPrincipal,
            StartedAtUtc: head.StartedAtUtc,
            CompletedAtUtc: head.CompletedAtUtc,
            SkipPromotionPathReason: head.SkipPromotionPathReason,
            OverrideFreezeReason: head.OverrideFreezeReason,
            Leaves: leaves);
    }
}
