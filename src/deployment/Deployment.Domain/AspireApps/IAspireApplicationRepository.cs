using Deployment.Domain.Abstractions;

namespace Deployment.Domain.AspireApps;

public interface IAspireApplicationRepository : IRepository<AspireApplication, Guid>
{
    Task<AspireApplication?> FindByNameAsync(string name, CancellationToken cancellationToken = default);
}
