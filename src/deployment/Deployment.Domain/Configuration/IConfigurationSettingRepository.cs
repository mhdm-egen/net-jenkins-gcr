using Deployment.Domain.Abstractions;

namespace Deployment.Domain.Configuration;

public interface IConfigurationSettingRepository : IRepository<ConfigurationSetting, Guid>
{
    /// <summary>
    /// Loads every setting that could resolve for <paramref name="deployableUnitId"/>
    /// when deploying into <paramref name="environmentId"/>. Includes both the
    /// env-specific overrides (EnvironmentId = @environmentId) and the unit-wide
    /// defaults (EnvironmentId IS NULL). The caller (resolver) walks the Rule X
    /// ladder over this in-memory set.
    /// </summary>
    Task<IReadOnlyList<ConfigurationSetting>> GetForUnitAsync(
        Guid deployableUnitId,
        Guid environmentId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Find one specific setting by its identifying tuple. Returns null if
    /// no matching row exists. Used by the upsert-style change commands.
    /// </summary>
    Task<ConfigurationSetting?> FindAsync(
        Guid deployableUnitId,
        Guid? environmentId,
        string key,
        CancellationToken cancellationToken = default);
}
