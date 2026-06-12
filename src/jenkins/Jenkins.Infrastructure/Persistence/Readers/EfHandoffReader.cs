using Jenkins.Application.Features.Handoffs;
using Jenkins.Contracts.Handoffs;
using Microsoft.EntityFrameworkCore;

namespace Jenkins.Infrastructure.Persistence.Readers;

/// <summary>Read-model reader for handoffs — flat projections to the wire DTO.</summary>
public sealed class EfHandoffReader : IHandoffReader
{
    private readonly JenkinsCiDbContext _db;

    public EfHandoffReader(JenkinsCiDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<ContainerReleaseHandoffDto>> ListByBuildAsync(
        Guid buildId, CancellationToken cancellationToken = default)
    {
        var query =
            from h in _db.Handoffs.AsNoTracking()
            where h.BuildId == buildId
            orderby h.CreatedAtUtc descending
            select Project(h);

        return await query.ToListAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<ContainerReleaseHandoffDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _db.Handoffs.AsNoTracking()
            .Where(h => h.Id == id)
            .Select(h => Project(h))
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    private static ContainerReleaseHandoffDto Project(Domain.Handoffs.ContainerReleaseHandoff h) => new(
        h.Id,
        h.BuildId,
        h.BuildArtifactId,
        h.DeployableComponentId,
        h.RepositoryId,
        h.DeployableUnitId,
        h.DeploymentReleaseId,
        h.SemanticVersion,
        h.ArtifactUri,
        (HandoffStatusDto)(int)h.Status,
        h.RequestedByPrincipal,
        h.CreatedAtUtc,
        h.SettledAtUtc,
        h.FailureReason);
}
