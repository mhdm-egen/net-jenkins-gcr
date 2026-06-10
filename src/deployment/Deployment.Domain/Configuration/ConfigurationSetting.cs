using Deployment.Domain.Common;
using Deployment.Domain.Configuration.Events;

namespace Deployment.Domain.Configuration;

/// <summary>
/// A single configuration row scoped to a <c>DeployableUnit</c> and optionally
/// an <c>Environment</c> (null = unit-wide default). Resolved at deploy time
/// per the Rule X ladder (decisions §4.1). One aggregate per row — settings
/// are independently editable and individually audited.
///
/// Invariant: secret/plain dichotomy
///   IsSecret = true  ⇒ Value = null AND SecretReference is non-empty.
///   IsSecret = false ⇒ SecretReference = null AND Value is non-null
///                      (empty string allowed if the caller really means "").
/// </summary>
public sealed class ConfigurationSetting : AggregateRoot<Guid>
{
    public Guid DeployableUnitId { get; private set; }
    public Guid? EnvironmentId { get; private set; }
    public string Key { get; private set; }
    public string? Value { get; private set; }
    public bool IsSecret { get; private set; }
    public string? SecretReference { get; private set; }
    public ConfigurationValueType ValueType { get; private set; }

    private ConfigurationSetting()
    {
        Key = string.Empty;
    }

    private ConfigurationSetting(
        Guid id,
        Guid deployableUnitId,
        Guid? environmentId,
        string key,
        string? value,
        bool isSecret,
        string? secretReference,
        ConfigurationValueType valueType)
    {
        if (id == Guid.Empty) throw new ArgumentException("Id cannot be empty.", nameof(id));
        if (deployableUnitId == Guid.Empty)
            throw new ArgumentException("DeployableUnitId cannot be empty.", nameof(deployableUnitId));
        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("Key cannot be empty.", nameof(key));

        ValidateSecretInvariant(isSecret, value, secretReference);

        Id = id;
        DeployableUnitId = deployableUnitId;
        EnvironmentId = environmentId;
        Key = key.Trim();
        Value = value;
        IsSecret = isSecret;
        SecretReference = string.IsNullOrWhiteSpace(secretReference) ? null : secretReference.Trim();
        ValueType = valueType;
    }

    public static ConfigurationSetting CreatePlain(
        Guid id,
        Guid deployableUnitId,
        Guid? environmentId,
        string key,
        string value,
        ConfigurationValueType valueType,
        string changedByPrincipal,
        DateTimeOffset occurredAtUtc)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (string.IsNullOrWhiteSpace(changedByPrincipal))
            throw new ArgumentException("ChangedByPrincipal cannot be empty.", nameof(changedByPrincipal));

        var s = new ConfigurationSetting(id, deployableUnitId, environmentId, key,
            value, isSecret: false, secretReference: null, valueType);

        s.RaiseEvent(new ConfigurationSettingChanged(
            s.Id, s.DeployableUnitId, s.EnvironmentId, s.Key,
            OldValue: null, OldSecretReference: null, OldIsSecret: null, OldValueType: null,
            NewValue: s.Value, NewSecretReference: null, NewIsSecret: false, NewValueType: s.ValueType,
            ConfigurationChangeKind.Created, changedByPrincipal.Trim(), occurredAtUtc));
        return s;
    }

    public static ConfigurationSetting CreateSecret(
        Guid id,
        Guid deployableUnitId,
        Guid? environmentId,
        string key,
        string secretReference,
        ConfigurationValueType valueType,
        string changedByPrincipal,
        DateTimeOffset occurredAtUtc)
    {
        if (string.IsNullOrWhiteSpace(secretReference))
            throw new ArgumentException("SecretReference cannot be empty for a secret setting.", nameof(secretReference));
        if (string.IsNullOrWhiteSpace(changedByPrincipal))
            throw new ArgumentException("ChangedByPrincipal cannot be empty.", nameof(changedByPrincipal));

        var s = new ConfigurationSetting(id, deployableUnitId, environmentId, key,
            value: null, isSecret: true, secretReference, valueType);

        s.RaiseEvent(new ConfigurationSettingChanged(
            s.Id, s.DeployableUnitId, s.EnvironmentId, s.Key,
            OldValue: null, OldSecretReference: null, OldIsSecret: null, OldValueType: null,
            NewValue: null, NewSecretReference: s.SecretReference, NewIsSecret: true, NewValueType: s.ValueType,
            ConfigurationChangeKind.Created, changedByPrincipal.Trim(), occurredAtUtc));
        return s;
    }

    /// <summary>
    /// Update the row in place. Pass the full new state — the method computes
    /// the diff and emits a single <c>Updated</c> event (no event if nothing
    /// actually changed). Caller chooses any combination: plain→plain, plain→secret,
    /// secret→plain, secret→secret.
    /// </summary>
    public void Update(
        string? newValue,
        bool newIsSecret,
        string? newSecretReference,
        ConfigurationValueType newValueType,
        string changedByPrincipal,
        DateTimeOffset occurredAtUtc)
    {
        if (string.IsNullOrWhiteSpace(changedByPrincipal))
            throw new ArgumentException("ChangedByPrincipal cannot be empty.", nameof(changedByPrincipal));

        ValidateSecretInvariant(newIsSecret, newValue, newSecretReference);

        var trimmedRef = string.IsNullOrWhiteSpace(newSecretReference) ? null : newSecretReference.Trim();

        var noChange =
            string.Equals(Value, newValue, StringComparison.Ordinal) &&
            IsSecret == newIsSecret &&
            string.Equals(SecretReference, trimmedRef, StringComparison.Ordinal) &&
            ValueType == newValueType;
        if (noChange) return;

        var oldValue = Value;
        var oldIsSecret = IsSecret;
        var oldRef = SecretReference;
        var oldType = ValueType;

        Value = newValue;
        IsSecret = newIsSecret;
        SecretReference = trimmedRef;
        ValueType = newValueType;

        RaiseEvent(new ConfigurationSettingChanged(
            Id, DeployableUnitId, EnvironmentId, Key,
            oldValue, oldRef, oldIsSecret, oldType,
            Value, SecretReference, IsSecret, ValueType,
            ConfigurationChangeKind.Updated, changedByPrincipal.Trim(), occurredAtUtc));
    }

    /// <summary>
    /// Emit the deletion event. The aggregate must then be removed via the
    /// repository; the UnitOfWork will dispatch this event after the row is
    /// physically deleted (events are snapshotted before <c>SaveChanges</c>).
    /// </summary>
    public void MarkForDeletion(string changedByPrincipal, DateTimeOffset occurredAtUtc)
    {
        if (string.IsNullOrWhiteSpace(changedByPrincipal))
            throw new ArgumentException("ChangedByPrincipal cannot be empty.", nameof(changedByPrincipal));

        RaiseEvent(new ConfigurationSettingChanged(
            Id, DeployableUnitId, EnvironmentId, Key,
            OldValue: Value, OldSecretReference: SecretReference, OldIsSecret: IsSecret, OldValueType: ValueType,
            NewValue: null, NewSecretReference: null, NewIsSecret: null, NewValueType: null,
            ConfigurationChangeKind.Deleted, changedByPrincipal.Trim(), occurredAtUtc));
    }

    private static void ValidateSecretInvariant(bool isSecret, string? value, string? secretReference)
    {
        if (isSecret)
        {
            if (value is not null)
                throw new InvalidOperationException("A secret setting must have a null Value.");
            if (string.IsNullOrWhiteSpace(secretReference))
                throw new InvalidOperationException("A secret setting requires a non-empty SecretReference.");
        }
        else
        {
            if (value is null)
                throw new InvalidOperationException("A plain setting must have a non-null Value.");
            if (!string.IsNullOrWhiteSpace(secretReference))
                throw new InvalidOperationException("A plain setting must have a null SecretReference.");
        }
    }
}
