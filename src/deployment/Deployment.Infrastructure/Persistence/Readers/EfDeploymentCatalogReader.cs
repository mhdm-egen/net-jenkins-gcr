using Deployment.Application.Features.Deployments.ListDeployments;
using Deployment.Contracts.Deployments;
using Deployment.Domain.Deployments;
using Microsoft.EntityFrameworkCore;

namespace Deployment.Infrastructure.Persistence.Readers;

internal sealed class EfDeploymentCatalogReader : IDeploymentCatalogReader
{
    private readonly DeploymentDbContext _db;

    public EfDeploymentCatalogReader(DeploymentDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<DeploymentSummaryDto>> ListAsync(
        Guid? environmentId,
        DeploymentStatusDto? status,
        Guid? releaseId,
        bool onlyParents,
        int take,
        CancellationToken cancellationToken = default)
    {
        if (take <= 0) take = 50;

        IQueryable<Domain.Deployments.Deployment> q = _db.Deployments.AsNoTracking();
        if (environmentId is { } eid) q = q.Where(d => d.EnvironmentId == eid);
        if (releaseId is { } rid) q = q.Where(d => d.ReleaseId == rid);
        if (status is { } s)
        {
            var domainStatus = (DeploymentStatus)(int)s;
            q = q.Where(d => d.Status == domainStatus);
        }
        if (onlyParents)
        {
            // A "logical" deployment event is the row with no parent (either a
            // standalone leaf or a cascade head). Excludes cascade child rows.
            q = q.Where(d => d.ParentDeploymentId == null);
        }

        var summaries = await BuildSummariesAsync(q.OrderByDescending(d => d.StartedAtUtc ?? DateTimeOffset.MaxValue)
            .ThenByDescending(d => d.CompletedAtUtc ?? DateTimeOffset.MaxValue)
            .Take(take), cancellationToken).ConfigureAwait(false);

        return summaries;
    }

    public async Task<DeploymentDetailDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var head = await _db.Deployments.AsNoTracking().FirstOrDefaultAsync(d => d.Id == id, cancellationToken)
            .ConfigureAwait(false);
        if (head is null) return null;

        // Head + cascade children (same parent, plus the head if it's a child) summarized.
        var cascadeIds = new List<Guid> { head.Id };
        if (head.ParentDeploymentId is null)
        {
            // Head is a parent: gather direct children.
            var childIds = await _db.Deployments.AsNoTracking()
                .Where(d => d.ParentDeploymentId == head.Id)
                .Select(d => d.Id)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);
            cascadeIds.AddRange(childIds);
        }

        var summaries = await BuildSummariesAsync(
            _db.Deployments.AsNoTracking().Where(d => cascadeIds.Contains(d.Id)),
            cancellationToken).ConfigureAwait(false);
        var headSummary = summaries.First(s => s.Id == head.Id);
        var children = summaries.Where(s => s.Id != head.Id).ToList();

        var approvals = await _db.Approvals.AsNoTracking()
            .Where(a => a.DeploymentId == id)
            .OrderBy(a => a.Id)
            .Select(a => new ApprovalDto(
                a.Id, a.DeploymentId, a.ApproverPrincipal,
                (ApprovalStatusDto)(int)a.Status, a.DecidedAtUtc, a.Comment))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var events = await _db.DeploymentEvents.AsNoTracking()
            .Where(e => e.DeploymentId == id)
            .OrderBy(e => e.Timestamp)
            .Select(e => new DeploymentEventDto(
                e.Id, e.DeploymentId, e.Timestamp, e.EventType, e.Detail))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var bindings = await (
            from b in _db.DeploymentSecretBindings.AsNoTracking()
            join s in _db.ConfigurationSettings.AsNoTracking() on b.ConfigurationSettingId equals s.Id
            where b.DeploymentId == id
            orderby s.Key
            select new DeploymentSecretBindingDto(
                b.DeploymentId, b.ConfigurationSettingId, s.Key, b.ResolvedSecretUri, b.ResolvedAtUtc)
        ).ToListAsync(cancellationToken).ConfigureAwait(false);

        return new DeploymentDetailDto(
            Head: headSummary,
            FailureReason: head.FailureReason,
            CancellationReason: head.CancellationReason,
            SkipPromotionPathReason: head.SkipPromotionPathReason,
            OverrideFreezeReason: head.OverrideFreezeReason,
            RolledBackByDeploymentId: head.RolledBackByDeploymentId,
            Children: children,
            Approvals: approvals,
            Events: events,
            SecretBindings: bindings);
    }

    /// <summary>
    /// Builds <see cref="DeploymentSummaryDto"/> rows from a deployment query,
    /// hydrating release/unit/env/target names in a single follow-up batch per
    /// dimension so the list view stays cheap regardless of cardinality.
    /// </summary>
    private async Task<IReadOnlyList<DeploymentSummaryDto>> BuildSummariesAsync(
        IQueryable<Domain.Deployments.Deployment> source,
        CancellationToken cancellationToken)
    {
        var deploys = await source
            .Select(d => new
            {
                d.Id, d.ReleaseId, d.EnvironmentId, d.TargetId, d.ParentDeploymentId,
                d.Status, d.Strategy, d.Trigger, d.TriggeredByPrincipal,
                d.StartedAtUtc, d.CompletedAtUtc,
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if (deploys.Count == 0) return Array.Empty<DeploymentSummaryDto>();

        var releaseIds = deploys.Select(d => d.ReleaseId).Distinct().ToList();
        var envIds = deploys.Select(d => d.EnvironmentId).Distinct().ToList();
        var targetIds = deploys.Where(d => d.TargetId.HasValue).Select(d => d.TargetId!.Value).Distinct().ToList();

        var releases = await _db.Releases.AsNoTracking()
            .Where(r => releaseIds.Contains(r.Id))
            .Select(r => new { r.Id, r.SemanticVersion, r.DeployableUnitId })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var unitIds = releases.Select(r => r.DeployableUnitId).Distinct().ToList();

        var unitNames = await _db.DeployableUnits.AsNoTracking()
            .Where(u => unitIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.Name, cancellationToken)
            .ConfigureAwait(false);

        var envNames = await _db.Environments.AsNoTracking()
            .Where(e => envIds.Contains(e.Id))
            .ToDictionaryAsync(e => e.Id, e => e.Name, cancellationToken)
            .ConfigureAwait(false);

        var targetResources = targetIds.Count == 0
            ? new Dictionary<Guid, string>()
            : await _db.DeploymentTargets.AsNoTracking()
                .Where(t => targetIds.Contains(t.Id))
                .ToDictionaryAsync(t => t.Id, t => t.ResourceId, cancellationToken)
                .ConfigureAwait(false);

        var releaseLookup = releases.ToDictionary(r => r.Id);

        return deploys.Select(d =>
        {
            var rel = releaseLookup[d.ReleaseId];
            return new DeploymentSummaryDto(
                Id: d.Id,
                ReleaseId: d.ReleaseId,
                ReleaseSemanticVersion: rel.SemanticVersion,
                DeployableUnitId: rel.DeployableUnitId,
                DeployableUnitName: unitNames.TryGetValue(rel.DeployableUnitId, out var un) ? un : "(unknown)",
                EnvironmentId: d.EnvironmentId,
                EnvironmentName: envNames.TryGetValue(d.EnvironmentId, out var en) ? en : "(unknown)",
                TargetId: d.TargetId,
                TargetResourceId: d.TargetId is { } tid && targetResources.TryGetValue(tid, out var trid) ? trid : null,
                ParentDeploymentId: d.ParentDeploymentId,
                Status: (DeploymentStatusDto)(int)d.Status,
                Strategy: (DeploymentStrategyDto)(int)d.Strategy,
                Trigger: (DeploymentTriggerDto)(int)d.Trigger,
                TriggeredByPrincipal: d.TriggeredByPrincipal,
                StartedAtUtc: d.StartedAtUtc,
                CompletedAtUtc: d.CompletedAtUtc);
        }).ToList();
    }
}
