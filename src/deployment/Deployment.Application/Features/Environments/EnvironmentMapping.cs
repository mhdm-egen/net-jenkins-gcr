using Deployment.Contracts.Environments;
using Deployment.Domain.Environments;
using Environment = Deployment.Domain.Environments.Environment;

namespace Deployment.Application.Features.Environments;

internal static class EnvironmentMapping
{
    public static EnvironmentDto ToDto(this Environment e) => new(
        Id: e.Id,
        Name: e.Name,
        PromotionRank: e.PromotionRank,
        RequiresApproval: e.RequiresApproval,
        IsProduction: e.IsProduction,
        Targets: e.Targets
            .OrderBy(t => t.Region).ThenBy(t => t.ResourceId)
            .Select(t => new DeploymentTargetDto(
                t.Id, t.EnvironmentId,
                (TargetKindDto)(int)t.TargetKind,
                t.ResourceId, t.Region, t.Slot))
            .ToList(),
        FreezeWindows: e.FreezeWindows
            .OrderByDescending(w => w.StartUtc)
            .Select(w => new EnvironmentFreezeWindowDto(
                w.Id, w.EnvironmentId, w.StartUtc, w.EndUtc,
                w.Reason, w.CreatedByPrincipal, w.CreatedAtUtc))
            .ToList());

    public static TargetKind ToDomain(this TargetKindDto k) => (TargetKind)(int)k;
}
