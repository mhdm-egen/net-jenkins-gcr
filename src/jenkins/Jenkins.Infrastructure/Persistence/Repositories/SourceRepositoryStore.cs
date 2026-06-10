using Jenkins.Domain.SourceRepositories;
using Microsoft.EntityFrameworkCore;

namespace Jenkins.Infrastructure.Persistence.Repositories;

internal sealed class SourceRepositoryStore : EfRepository<SourceRepository, Guid>, ISourceRepositoryStore
{
    public SourceRepositoryStore(JenkinsCiDbContext db) : base(db) { }

    // Components are AutoInclude'd, but be explicit so the write path is obvious.
    public override Task<SourceRepository?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => Set.Include(r => r.Components).FirstOrDefaultAsync(r => r.Id == id, cancellationToken);

    public Task<SourceRepository?> FindByNameAsync(string name, CancellationToken cancellationToken = default)
        => Set.Include(r => r.Components).FirstOrDefaultAsync(r => r.Name == name, cancellationToken);
}
