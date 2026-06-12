using Jenkins.Domain.PipelineRuns;
using Microsoft.EntityFrameworkCore;

namespace Jenkins.Infrastructure.Persistence.Repositories;

public sealed class PipelineRunStore : EfRepository<PipelineRun, Guid>, IPipelineRunStore
{
    public PipelineRunStore(JenkinsCiDbContext db) : base(db) { }

    public override Task<PipelineRun?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => Set.FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
}
