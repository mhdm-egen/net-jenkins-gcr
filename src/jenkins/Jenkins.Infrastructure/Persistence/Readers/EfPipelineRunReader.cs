using Jenkins.Application.Features.PipelineRuns;
using Jenkins.Contracts.PipelineRuns;
using Microsoft.EntityFrameworkCore;

namespace Jenkins.Infrastructure.Persistence.Readers;

/// <summary>
/// Read-model for pipeline runs. Steps live in a JSON column (not separately queryable), so
/// these load the (no-tracking) entities and map in memory rather than projecting in SQL.
/// </summary>
internal sealed class EfPipelineRunReader : IPipelineRunReader
{
    private readonly JenkinsCiDbContext _db;

    public EfPipelineRunReader(JenkinsCiDbContext db) => _db = db;

    public async Task<IReadOnlyList<PipelineRunSummaryDto>> ListAsync(Guid? pipelineId, int take, CancellationToken cancellationToken = default)
    {
        var query = _db.PipelineRuns.AsNoTracking();
        if (pipelineId is { } pid) query = query.Where(r => r.PipelineId == pid);

        var runs = await query
            .OrderByDescending(r => r.StartedAtUtc)
            .Take(take)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        return runs.Select(r => new PipelineRunSummaryDto(
            r.Id, r.PipelineId, r.PipelineName, (PipelineRunStatusDto)(int)r.Status,
            r.StartedAtUtc, r.CompletedAtUtc, r.Steps.Count)).ToList();
    }

    public async Task<PipelineRunDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var r = await _db.PipelineRuns.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken).ConfigureAwait(false);
        if (r is null) return null;

        return new PipelineRunDto(
            r.Id, r.PipelineId, r.PipelineName, r.RepositoryId, r.TriggeredBy,
            (PipelineRunStatusDto)(int)r.Status, r.StartedAtUtc, r.CompletedAtUtc, r.FailureReason,
            r.Steps.Select(s => new PipelineRunStepDto(s.Order, s.JobName, s.BuildNumber, s.Result)).ToList());
    }
}
