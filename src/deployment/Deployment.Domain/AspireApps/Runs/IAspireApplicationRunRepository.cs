using Deployment.Domain.Abstractions;

namespace Deployment.Domain.AspireApps.Runs;

public interface IAspireApplicationRunRepository : IRepository<AspireApplicationRun, Guid>
{
}
