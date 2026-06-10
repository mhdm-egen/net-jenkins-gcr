using Deployment.Domain.Configuration;

namespace Deployment.Application.Features.Configuration.ResolveEffectiveConfig;

/// <summary>
/// Resolution ladder (decisions §4.1):
/// <list type="number">
///   <item>App + env-specific</item>
///   <item>Service + env-specific</item>
///   <item>App + unit-default</item>
///   <item>Service + unit-default</item>
/// </list>
/// First match wins per key. Env always trumps unit-default within the same
/// owner tier; App always trumps Service within the same env tier.
/// </summary>
public sealed class ResolveEffectiveConfigHandler
{
    private readonly IConfigurationSettingRepository _settings;

    public ResolveEffectiveConfigHandler(IConfigurationSettingRepository settings)
    {
        _settings = settings;
    }

    public async Task<EffectiveConfig> HandleAsync(
        ResolveEffectiveConfigQuery query,
        CancellationToken cancellationToken = default)
    {
        var serviceRows = await _settings.GetForUnitAsync(query.ServiceId, query.EnvironmentId, cancellationToken)
            .ConfigureAwait(false);

        IReadOnlyList<ConfigurationSetting> appRows = query.ApplicationId is { } appId
            ? await _settings.GetForUnitAsync(appId, query.EnvironmentId, cancellationToken).ConfigureAwait(false)
            : Array.Empty<ConfigurationSetting>();

        // Bucket each owner's rows into env-specific vs unit-default for O(1) lookups.
        var (appEnv, appDefault) = Partition(appRows, query.EnvironmentId);
        var (svcEnv, svcDefault) = Partition(serviceRows, query.EnvironmentId);

        var keys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var s in appRows) keys.Add(s.Key);
        foreach (var s in serviceRows) keys.Add(s.Key);

        var resolved = new List<EffectiveConfigEntry>(keys.Count);
        foreach (var key in keys)
        {
            EffectiveConfigEntry? hit =
                TryPick(appEnv, key, ConfigOrigin.ApplicationEnvironment)
                ?? TryPick(svcEnv, key, ConfigOrigin.ServiceEnvironment)
                ?? TryPick(appDefault, key, ConfigOrigin.ApplicationDefault)
                ?? TryPick(svcDefault, key, ConfigOrigin.ServiceDefault);

            if (hit is not null) resolved.Add(hit);
        }

        return new EffectiveConfig(resolved);
    }

    private static (Dictionary<string, ConfigurationSetting> Env, Dictionary<string, ConfigurationSetting> Default)
        Partition(IEnumerable<ConfigurationSetting> rows, Guid environmentId)
    {
        var env = new Dictionary<string, ConfigurationSetting>(StringComparer.Ordinal);
        var def = new Dictionary<string, ConfigurationSetting>(StringComparer.Ordinal);
        foreach (var r in rows)
        {
            if (r.EnvironmentId == environmentId) env[r.Key] = r;
            else if (r.EnvironmentId is null) def[r.Key] = r;
        }
        return (env, def);
    }

    private static EffectiveConfigEntry? TryPick(
        Dictionary<string, ConfigurationSetting> bucket,
        string key,
        ConfigOrigin origin)
    {
        if (!bucket.TryGetValue(key, out var row)) return null;
        return new EffectiveConfigEntry(
            row.Id, row.Key, row.Value, row.IsSecret, row.SecretReference, row.ValueType, origin);
    }
}
