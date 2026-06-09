using Deployment.Application.Abstractions;
using Deployment.Application.Runner;
using Deployment.Contracts.Deployments;
using Deployment.Contracts.Environments;
using Deployment.Contracts.Releases;
using Deployment.Domain.Deployments;
using Deployment.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Deployment.Infrastructure.Runner;

internal sealed class EfDeploymentRunnerReader : IDeploymentRunnerReader
{
    private readonly DeploymentDbContext _db;

    public EfDeploymentRunnerReader(DeploymentDbContext db)
    {
        _db = db;
    }

    public async Task<Guid?> FindNextQueuedLeafAsync(CancellationToken cancellationToken = default)
    {
        // Leaves only: TargetId IS NOT NULL. Cascade parents are advanced by
        // the rollup helper as children terminate, not by the runner directly.
        // Oldest-first by Id (we don't have a queue-time column distinct from
        // the row id, but Guid V4 ordering is good enough for round-robin in
        // v1; future enhancement: claim via OUTPUT INSERTED.Id).
        return await _db.Deployments.AsNoTracking()
            .Where(d => d.Status == DeploymentStatus.Queued && d.TargetId != null)
            .OrderBy(d => d.Id)
            .Select(d => (Guid?)d.Id)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<DeploymentExecutionContext?> GetExecutionContextAsync(
        Guid deploymentId, CancellationToken cancellationToken = default)
    {
        var snapshot = await (
            from d in _db.Deployments.AsNoTracking()
            join r in _db.Releases.AsNoTracking() on d.ReleaseId equals r.Id
            join u in _db.DeployableUnits.AsNoTracking() on r.DeployableUnitId equals u.Id
            join t in _db.DeploymentTargets.AsNoTracking() on d.TargetId equals t.Id
            where d.Id == deploymentId
            select new
            {
                d.Id, d.ReleaseId, ReleaseVersion = r.SemanticVersion,
                r.ArtifactType, r.ArtifactUri,
                r.DeployableUnitId, UnitName = u.Name,
                d.Strategy,
                Target = new { t.Id, t.TargetKind, t.ResourceId, t.Region, t.Slot },
            })
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        if (snapshot is null) return null;

        var bindings = await (
            from b in _db.DeploymentSecretBindings.AsNoTracking()
            join s in _db.ConfigurationSettings.AsNoTracking() on b.ConfigurationSettingId equals s.Id
            where b.DeploymentId == deploymentId
            select new ResolvedSecretBinding(b.ConfigurationSettingId, s.Key, b.ResolvedSecretUri))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return new DeploymentExecutionContext(
            DeploymentId: snapshot.Id,
            ReleaseId: snapshot.ReleaseId,
            ReleaseSemanticVersion: snapshot.ReleaseVersion,
            ArtifactType: (ArtifactTypeDto)(int)snapshot.ArtifactType,
            ArtifactUri: snapshot.ArtifactUri,
            DeployableUnitId: snapshot.DeployableUnitId,
            DeployableUnitName: snapshot.UnitName,
            Target: new DeploymentTargetDescriptor(
                snapshot.Target.Id,
                (TargetKindDto)(int)snapshot.Target.TargetKind,
                snapshot.Target.ResourceId,
                snapshot.Target.Region,
                snapshot.Target.Slot),
            Strategy: (DeploymentStrategyDto)(int)snapshot.Strategy,
            SecretBindings: bindings);
    }
}
