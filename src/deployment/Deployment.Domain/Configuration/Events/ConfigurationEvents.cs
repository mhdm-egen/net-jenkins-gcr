using Deployment.Domain.Common;

namespace Deployment.Domain.Configuration.Events;

/// <summary>
/// The single change event for a <see cref="ConfigurationSetting"/>. Captures
/// the full before/after snapshot — drives the <c>ConfigurationSettingHistory</c>
/// projection (decisions §4.2). On Created the Old* fields are null; on Deleted
/// the New* fields are null.
/// </summary>
public sealed record ConfigurationSettingChanged(
    Guid ConfigurationSettingId,
    Guid DeployableUnitId,
    Guid? EnvironmentId,
    string Key,
    string? OldValue,
    string? OldSecretReference,
    bool? OldIsSecret,
    ConfigurationValueType? OldValueType,
    string? NewValue,
    string? NewSecretReference,
    bool? NewIsSecret,
    ConfigurationValueType? NewValueType,
    ConfigurationChangeKind ChangeKind,
    string ChangedByPrincipal,
    DateTimeOffset OccurredAtUtc) : IDomainEvent;
