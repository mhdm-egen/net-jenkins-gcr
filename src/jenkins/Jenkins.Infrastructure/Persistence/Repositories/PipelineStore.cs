using Jenkins.Domain.Pipelines;
using Microsoft.EntityFrameworkCore;

namespace Jenkins.Infrastructure.Persistence.Repositories;

internal sealed class PipelineStore : EfRepository<Pipeline, Guid>, IPipelineStore
{
    public PipelineStore(JenkinsCiDbContext db) : base(db) { }

    // Stages are AutoInclude'd via the "_stages" backing-field navigation.
    public override Task<Pipeline?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => Set.FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

    public Task<Pipeline?> FindByNameAsync(string name, CancellationToken cancellationToken = default)
        => Set.FirstOrDefaultAsync(p => p.Name == name, cancellationToken);

    public Task<bool> AnyAsync(CancellationToken cancellationToken = default)
        => Set.AnyAsync(cancellationToken);
}
