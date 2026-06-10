using Jenkins.Domain.Abstractions;

namespace Jenkins.Domain.SourceRepositories;

/// <summary>
/// Persistence seam for the <see cref="SourceRepository"/> aggregate. Concrete impl
/// lives in <c>Jenkins.Infrastructure.Persistence.Repositories</c>.
/// </summary>
/// <remarks>
/// Suffixed <c>Store</c> rather than <c>Repository</c> to avoid the stutter
/// (<c>ISourceRepositoryRepository</c>) — the aggregate name already ends in
/// "Repository". The deployment service's <c>I{Aggregate}Repository</c> convention
/// applies to the other aggregates (Build, Handoff) where it doesn't stutter.
/// </remarks>
public interface ISourceRepositoryStore : IRepository<SourceRepository, Guid>
{
    /// <summary>Enforces the unique-name invariant on register (matches the unique index).</summary>
    Task<SourceRepository?> FindByNameAsync(string name, CancellationToken cancellationToken = default);
}
