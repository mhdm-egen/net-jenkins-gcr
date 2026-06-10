using Jenkins.Domain.Abstractions;

namespace Jenkins.Domain.Pipelines;

/// <summary>
/// Persistence seam for the <see cref="Pipeline"/> aggregate. Concrete impl lives
/// in <c>Jenkins.Infrastructure.Persistence.Repositories</c>.
/// </summary>
public interface IPipelineStore : IRepository<Pipeline, Guid>
{
    /// <summary>Unique-name guard on create; loads the full aggregate (stages).</summary>
    Task<Pipeline?> FindByNameAsync(string name, CancellationToken cancellationToken = default);

    /// <summary>True when at least one pipeline exists (used by the default seed).</summary>
    Task<bool> AnyAsync(CancellationToken cancellationToken = default);
}
