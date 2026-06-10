using Deployment.Domain.Configuration;
using Microsoft.EntityFrameworkCore;

namespace Deployment.Infrastructure.Persistence.Repositories;

internal sealed class ConfigurationSettingRepository
    : EfRepository<ConfigurationSetting, Guid>, IConfigurationSettingRepository
{
    public ConfigurationSettingRepository(DeploymentDbContext db) : base(db) { }

    public async Task<IReadOnlyList<ConfigurationSetting>> GetForUnitAsync(
        Guid deployableUnitId,
        Guid environmentId,
        CancellationToken cancellationToken = default)
    {
        return await Set
            .Where(s => s.DeployableUnitId == deployableUnitId
                        && (s.EnvironmentId == environmentId || s.EnvironmentId == null))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public Task<ConfigurationSetting?> FindAsync(
        Guid deployableUnitId,
        Guid? environmentId,
        string key,
        CancellationToken cancellationToken = default)
        => Set.FirstOrDefaultAsync(
            s => s.DeployableUnitId == deployableUnitId
                 && s.EnvironmentId == environmentId
                 && s.Key == key,
            cancellationToken);
}
