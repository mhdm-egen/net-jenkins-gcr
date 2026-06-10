using Deployment.Domain.Common;

namespace Deployment.Domain.Environments;

/// <summary>
/// A concrete place where a release can be installed inside an
/// <see cref="Environment"/>. Child entity of the Environment aggregate;
/// referenced by id from <c>Deployment.TargetId</c>.
///
/// <see cref="Slot"/> is optional and applies to slot-aware targets
/// (e.g., App Service deployment slots like "staging" vs "production").
/// </summary>
public sealed class DeploymentTarget : Entity<Guid>
{
    public Guid EnvironmentId { get; private set; }
    public TargetKind TargetKind { get; private set; }

    /// <summary>Adapter-specific resource identifier (e.g., a K8s namespace, ARM resource id).</summary>
    public string ResourceId { get; private set; }
    public string Region { get; private set; }
    public string? Slot { get; private set; }

    private DeploymentTarget()
    {
        ResourceId = string.Empty;
        Region = string.Empty;
    }

    internal DeploymentTarget(
        Guid id,
        Guid environmentId,
        TargetKind targetKind,
        string resourceId,
        string region,
        string? slot)
    {
        if (id == Guid.Empty) throw new ArgumentException("Id cannot be empty.", nameof(id));
        if (environmentId == Guid.Empty)
            throw new ArgumentException("EnvironmentId cannot be empty.", nameof(environmentId));
        if (string.IsNullOrWhiteSpace(resourceId))
            throw new ArgumentException("ResourceId cannot be empty.", nameof(resourceId));
        if (string.IsNullOrWhiteSpace(region))
            throw new ArgumentException("Region cannot be empty.", nameof(region));

        Id = id;
        EnvironmentId = environmentId;
        TargetKind = targetKind;
        ResourceId = resourceId.Trim();
        Region = region.Trim();
        Slot = string.IsNullOrWhiteSpace(slot) ? null : slot.Trim();
    }

    internal void Update(TargetKind targetKind, string resourceId, string region, string? slot)
    {
        if (string.IsNullOrWhiteSpace(resourceId))
            throw new ArgumentException("ResourceId cannot be empty.", nameof(resourceId));
        if (string.IsNullOrWhiteSpace(region))
            throw new ArgumentException("Region cannot be empty.", nameof(region));

        TargetKind = targetKind;
        ResourceId = resourceId.Trim();
        Region = region.Trim();
        Slot = string.IsNullOrWhiteSpace(slot) ? null : slot.Trim();
    }
}
