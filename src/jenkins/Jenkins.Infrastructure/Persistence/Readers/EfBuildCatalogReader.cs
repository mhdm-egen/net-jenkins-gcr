using Jenkins.Application.Features.Builds;
using Jenkins.Contracts.Builds;
using Microsoft.EntityFrameworkCore;

namespace Jenkins.Infrastructure.Persistence.Readers;

/// <summary>
/// Read-model reader for the build list — a flat projection to
/// <see cref="BuildSummaryDto"/>. Build detail (with artifacts/publications) is
/// served by loading the aggregate via <c>IBuildStore</c> and mapping in memory,
/// which avoids fragile EF translation of the nested collections + tags column.
/// </summary>
internal sealed class EfBuildCatalogReader : IBuildCatalogReader
{
    private readonly JenkinsCiDbContext _db;

    public EfBuildCatalogReader(JenkinsCiDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<BuildSummaryDto>> ListByRepositoryAsync(
        Guid repositoryId,
        int take,
        CancellationToken cancellationToken = default)
    {
        var capped = take <= 0 ? 50 : Math.Min(take, 500);

        var query =
            from b in _db.Builds.AsNoTracking()
            where b.RepositoryId == repositoryId
            orderby b.StartedAtUtc descending
            select new BuildSummaryDto(
                b.Id,
                b.RepositoryId,
                b.CiJobName,
                b.CiBuildNumber,
                b.CiRunUrl,
                b.SourceRevision.CommitShort,
                b.SourceRevision.Branch,
                b.Versions == null ? null : b.Versions.PackageVersion,
                (BuildStatusDto)(int)b.Status,
                b.StartedAtUtc,
                b.CompletedAtUtc);

        return await query.Take(capped).ToListAsync(cancellationToken).ConfigureAwait(false);
    }
}
