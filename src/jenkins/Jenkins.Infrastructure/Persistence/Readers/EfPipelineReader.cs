using Jenkins.Application.Features.Pipelines;
using Jenkins.Contracts.Pipelines;
using Microsoft.EntityFrameworkCore;

namespace Jenkins.Infrastructure.Persistence.Readers;

/// <summary>
/// Read-model reader for the pipeline list (flat summaries). Pipeline detail (with
/// stages + their params) loads the aggregate via <c>IPipelineStore</c> and maps in
/// memory, avoiding EF translation of the params JSON column.
/// </summary>
internal sealed class EfPipelineReader : IPipelineReader
{
    private readonly JenkinsCiDbContext _db;

    public EfPipelineReader(JenkinsCiDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<PipelineSummaryDto>> ListAsync(CancellationToken cancellationToken = default)
    {
        var query =
            from p in _db.Pipelines.AsNoTracking()
            orderby p.Name
            select new PipelineSummaryDto(
                p.Id,
                p.Name,
                p.Description,
                p.IsActive,
                p.CreatedAtUtc,
                _db.PipelineStages.Count(s => s.PipelineId == p.Id));

        return await query.ToListAsync(cancellationToken).ConfigureAwait(false);
    }
}
