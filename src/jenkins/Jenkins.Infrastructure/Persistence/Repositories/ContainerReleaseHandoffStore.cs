using Jenkins.Domain.Handoffs;
using Microsoft.EntityFrameworkCore;

namespace Jenkins.Infrastructure.Persistence.Repositories;

internal sealed class ContainerReleaseHandoffStore
    : EfRepository<ContainerReleaseHandoff, Guid>, IContainerReleaseHandoffStore
{
    public ContainerReleaseHandoffStore(JenkinsCiDbContext db) : base(db) { }

    public Task<ContainerReleaseHandoff?> FindLatestByArtifactAsync(
        Guid buildArtifactId, CancellationToken cancellationToken = default)
        => Set.Where(h => h.BuildArtifactId == buildArtifactId)
              .OrderByDescending(h => h.CreatedAtUtc)
              .FirstOrDefaultAsync(cancellationToken);
}
