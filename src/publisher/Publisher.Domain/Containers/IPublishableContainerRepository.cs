using Publisher.Domain.Abstractions;

namespace Publisher.Domain.Containers;

/// <summary>
/// Repository for the <see cref="PublishableContainer"/> aggregate. Adds the natural-key
/// lookup the ingest path needs to keep recording idempotent.
/// </summary>
public interface IPublishableContainerRepository : IRepository<PublishableContainer, Guid>
{
    /// <summary>
    /// Finds an existing inventory record by its natural key
    /// (RepositoryId + ContainerName + Version). Used to upsert on re-observation.
    /// </summary>
    Task<PublishableContainer?> FindByNaturalKeyAsync(
        Guid repositoryId,
        string containerName,
        string version,
        CancellationToken cancellationToken = default);
}
