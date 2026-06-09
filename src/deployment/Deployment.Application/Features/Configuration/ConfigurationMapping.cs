using Deployment.Contracts.Configuration;
using Deployment.Domain.Configuration;

namespace Deployment.Application.Features.Configuration;

internal static class ConfigurationMapping
{
    /// <summary>
    /// Projects a domain row + a denormalized env-name lookup into the wire DTO.
    /// </summary>
    public static ConfigurationSettingDto ToDto(
        this ConfigurationSetting s,
        IReadOnlyDictionary<Guid, string> envNames) => new(
            Id: s.Id,
            DeployableUnitId: s.DeployableUnitId,
            EnvironmentId: s.EnvironmentId,
            EnvironmentName: s.EnvironmentId is { } eid && envNames.TryGetValue(eid, out var n) ? n : null,
            Key: s.Key,
            Value: s.Value,
            IsSecret: s.IsSecret,
            SecretReference: s.SecretReference,
            ValueType: (ConfigurationValueTypeDto)(int)s.ValueType);

    public static ConfigurationValueType ToDomain(this ConfigurationValueTypeDto t) =>
        (ConfigurationValueType)(int)t;
}
