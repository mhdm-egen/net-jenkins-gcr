using Deployment.Domain.Abstractions;

namespace Deployment.Domain.Environments;

public interface IEnvironmentRepository : IRepository<Environment, Guid>
{
    Task<Environment?> FindByNameAsync(string name, CancellationToken cancellationToken = default);

    /// <summary>
    /// Loads the environment one rank below <paramref name="targetRank"/>, used
    /// by the implicit promotion-path check in <c>StartDeployment</c>.
    /// Returns null if there is no lower-ranked environment.
    /// </summary>
    Task<Environment?> FindByPromotionRankAsync(int targetRank, CancellationToken cancellationToken = default);
}
