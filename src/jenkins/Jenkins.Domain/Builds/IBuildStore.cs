using Jenkins.Domain.Abstractions;

namespace Jenkins.Domain.Builds;

/// <summary>
/// Persistence seam for the <see cref="Build"/> aggregate. Concrete impl lives in
/// <c>Jenkins.Infrastructure.Persistence.Repositories</c>.
/// </summary>
public interface IBuildStore : IRepository<Build, Guid>
{
    /// <summary>
    /// Look up a build by its natural CI key (job + number) — the upsert anchor
    /// for the Jenkins sync path. Loads the full aggregate (artifacts + publications).
    /// </summary>
    Task<Build?> FindByCiKeyAsync(string ciJobName, int ciBuildNumber, CancellationToken cancellationToken = default);
}
