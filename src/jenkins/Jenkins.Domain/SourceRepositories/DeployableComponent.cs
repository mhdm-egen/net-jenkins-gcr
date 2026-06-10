using Jenkins.Domain.Common;

namespace Jenkins.Domain.SourceRepositories;

/// <summary>
/// The container→deployment mapping (CI decision #2). One row per deployable
/// container image a repository produces. <see cref="ContainerName"/> is matched
/// against a build artifact's name to decide what gets handed off, and to which
/// deployment Service (<see cref="DeployableUnitId"/>). <see cref="AutoPublish"/>
/// is the per-component opt-in to hands-off promotion (CI decision #3).
///
/// Child entity of <see cref="SourceRepository"/>; created and mutated only via
/// the aggregate root.
/// </summary>
public sealed class DeployableComponent : Entity<Guid>
{
    public Guid RepositoryId { get; private set; }

    /// <summary>Image name the build produces (e.g. <c>egen/web-apphost</c>); the match key.</summary>
    public string ContainerName { get; private set; }

    /// <summary>The Service id in the deployment microservice this container maps to.</summary>
    public Guid DeployableUnitId { get; private set; }

    /// <summary>Cached display label for the deployment Service.</summary>
    public string DeployableUnitName { get; private set; }

    /// <summary>When true, a successful container push auto-creates a deployment Release.</summary>
    public bool AutoPublish { get; private set; }

    public bool IsActive { get; private set; }

    private DeployableComponent()
    {
        ContainerName = string.Empty;
        DeployableUnitName = string.Empty;
    }

    internal DeployableComponent(
        Guid id,
        Guid repositoryId,
        string containerName,
        Guid deployableUnitId,
        string deployableUnitName,
        bool autoPublish)
    {
        if (id == Guid.Empty) throw new ArgumentException("Id cannot be empty.", nameof(id));
        if (repositoryId == Guid.Empty)
            throw new ArgumentException("RepositoryId cannot be empty.", nameof(repositoryId));
        if (string.IsNullOrWhiteSpace(containerName))
            throw new ArgumentException("ContainerName cannot be empty.", nameof(containerName));
        if (deployableUnitId == Guid.Empty)
            throw new ArgumentException("DeployableUnitId cannot be empty.", nameof(deployableUnitId));
        if (string.IsNullOrWhiteSpace(deployableUnitName))
            throw new ArgumentException("DeployableUnitName cannot be empty.", nameof(deployableUnitName));

        Id = id;
        RepositoryId = repositoryId;
        ContainerName = containerName.Trim();
        DeployableUnitId = deployableUnitId;
        DeployableUnitName = deployableUnitName.Trim();
        AutoPublish = autoPublish;
        IsActive = true;
    }

    internal bool Remap(Guid deployableUnitId, string deployableUnitName, bool autoPublish)
    {
        if (deployableUnitId == Guid.Empty)
            throw new ArgumentException("DeployableUnitId cannot be empty.", nameof(deployableUnitId));
        if (string.IsNullOrWhiteSpace(deployableUnitName))
            throw new ArgumentException("DeployableUnitName cannot be empty.", nameof(deployableUnitName));

        var changed = DeployableUnitId != deployableUnitId
            || !string.Equals(DeployableUnitName, deployableUnitName.Trim(), StringComparison.Ordinal)
            || AutoPublish != autoPublish;

        DeployableUnitId = deployableUnitId;
        DeployableUnitName = deployableUnitName.Trim();
        AutoPublish = autoPublish;
        return changed;
    }

    internal void Deactivate() => IsActive = false;
    internal void Reactivate() => IsActive = true;
}
