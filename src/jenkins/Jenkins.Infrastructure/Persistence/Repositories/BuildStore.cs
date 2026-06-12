using Jenkins.Domain.Builds;
using Microsoft.EntityFrameworkCore;

namespace Jenkins.Infrastructure.Persistence.Repositories;

public sealed class BuildStore : EfRepository<Build, Guid>, IBuildStore
{
    public BuildStore(JenkinsCiDbContext db) : base(db) { }

    // Artifacts + publications are AutoInclude'd; explicit Include keeps the
    // write-path load obvious.
    public override Task<Build?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => Set.Include(b => b.Artifacts).ThenInclude(a => a.Publications)
              .FirstOrDefaultAsync(b => b.Id == id, cancellationToken);

    public Task<Build?> FindByCiKeyAsync(string ciJobName, int ciBuildNumber, CancellationToken cancellationToken = default)
        => Set.Include(b => b.Artifacts).ThenInclude(a => a.Publications)
              .FirstOrDefaultAsync(b => b.CiJobName == ciJobName && b.CiBuildNumber == ciBuildNumber, cancellationToken);
}
