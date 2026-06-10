using Deployment.Domain.Configuration;

namespace Deployment.Infrastructure.Persistence.Projections;

/// <summary>
/// Audit row for a single <c>ConfigurationSetting</c> change (decisions §4.2).
/// Append-only. Built by a Wolverine event handler subscribing to
/// <c>ConfigurationSettingChanged</c>. Lives in Infrastructure because it's
/// a read-model concern, not a domain invariant.
/// </summary>
public sealed class ConfigurationSettingHistoryRow
{
    public Guid HistoryId { get; init; }
    public Guid ConfigurationSettingId { get; init; }
    public ConfigurationChangeKind ChangeKind { get; init; }
    public string? OldValue { get; init; }
    public string? OldSecretReference { get; init; }
    public bool? OldIsSecret { get; init; }
    public ConfigurationValueType? OldValueType { get; init; }
    public string? NewValue { get; init; }
    public string? NewSecretReference { get; init; }
    public bool? NewIsSecret { get; init; }
    public ConfigurationValueType? NewValueType { get; init; }
    public string ChangedByPrincipal { get; init; } = string.Empty;
    public DateTimeOffset ChangedAtUtc { get; init; }
}
