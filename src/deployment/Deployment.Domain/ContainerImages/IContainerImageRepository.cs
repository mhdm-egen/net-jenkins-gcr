using Deployment.Domain.Abstractions;

namespace Deployment.Domain.ContainerImages;

/// <summary>
/// Persistence seam for the <see cref="ContainerImage"/> aggregate. Concrete impl
/// lives in <c>Deployment.Infrastructure.Persistence.Repositories</c>.
/// </summary>
public interface IContainerImageRepository : IRepository<ContainerImage, Guid>
{
    /// <summary>
    /// Resolve a coordinate (Registry + Repository + Name, case-insensitive). Used to
    /// upsert from the release-publish path and to enforce the unique-coordinate
    /// invariant (matches the unique index).
    /// </summary>
    Task<ContainerImage?> FindByCoordinateAsync(
        string registry, string repository, string name, CancellationToken cancellationToken = default);
}
