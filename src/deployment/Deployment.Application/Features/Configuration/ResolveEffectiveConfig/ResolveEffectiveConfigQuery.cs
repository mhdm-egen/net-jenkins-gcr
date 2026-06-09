using Deployment.Domain.Configuration;

namespace Deployment.Application.Features.Configuration.ResolveEffectiveConfig;

/// <summary>
/// Resolve the effective configuration for deploying a service (and optionally
/// an enclosing application context) into an environment. Implements Rule X
/// (decisions §4.1). Pure query.
///
/// <paramref name="ApplicationId"/> is null when resolving config for a
/// standalone service deploy (no enclosing app).
/// </summary>
public sealed record ResolveEffectiveConfigQuery(
    Guid ServiceId,
    Guid? ApplicationId,
    Guid EnvironmentId);

public sealed record EffectiveConfig(
    IReadOnlyList<EffectiveConfigEntry> Entries);

public sealed record EffectiveConfigEntry(
    Guid ConfigurationSettingId,
    string Key,
    string? Value,
    bool IsSecret,
    string? SecretReference,
    ConfigurationValueType ValueType,
    ConfigOrigin Origin);

public enum ConfigOrigin
{
    /// <summary>Application + env-specific (tier 1, highest).</summary>
    ApplicationEnvironment = 0,

    /// <summary>Service + env-specific (tier 2).</summary>
    ServiceEnvironment = 1,

    /// <summary>Application + unit-default (tier 3).</summary>
    ApplicationDefault = 2,

    /// <summary>Service + unit-default (tier 4, lowest).</summary>
    ServiceDefault = 3,
}
