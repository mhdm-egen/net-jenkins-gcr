using Deployment.Domain.Configuration;

namespace Deployment.Application.Tests.Fakes;

internal sealed class FakeConfigurationSettingRepository : IConfigurationSettingRepository
{
    private readonly Dictionary<Guid, ConfigurationSetting> _byId = new();

    public void Seed(ConfigurationSetting s) => _byId[s.Id] = s;

    public Task<ConfigurationSetting?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => Task.FromResult(_byId.GetValueOrDefault(id));

    public Task AddAsync(ConfigurationSetting aggregate, CancellationToken ct = default)
    {
        _byId[aggregate.Id] = aggregate;
        return Task.CompletedTask;
    }

    public void Remove(ConfigurationSetting aggregate) => _byId.Remove(aggregate.Id);

    public Task<IReadOnlyList<ConfigurationSetting>> GetForUnitAsync(
        Guid deployableUnitId, Guid environmentId, CancellationToken ct = default)
    {
        var hits = _byId.Values
            .Where(s => s.DeployableUnitId == deployableUnitId
                        && (s.EnvironmentId == environmentId || s.EnvironmentId == null))
            .ToList();
        return Task.FromResult<IReadOnlyList<ConfigurationSetting>>(hits);
    }

    public Task<ConfigurationSetting?> FindAsync(
        Guid deployableUnitId, Guid? environmentId, string key, CancellationToken ct = default)
    {
        var hit = _byId.Values.FirstOrDefault(s =>
            s.DeployableUnitId == deployableUnitId
            && s.EnvironmentId == environmentId
            && s.Key == key);
        return Task.FromResult<ConfigurationSetting?>(hit);
    }
}
