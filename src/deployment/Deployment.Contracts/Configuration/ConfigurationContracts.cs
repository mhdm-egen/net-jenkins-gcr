namespace Deployment.Contracts.Configuration;

// Wire-stable mirrors of the Configuration enums. Integer values must match
// Deployment.Domain.Configuration enums one-for-one.

public enum ConfigurationValueTypeDto
{
    String = 0,
    Int = 1,
    Bool = 2,
    Json = 3,
}

public enum ConfigurationChangeKindDto
{
    Created = 0,
    Updated = 1,
    Deleted = 2,
}

// --- Read-side DTOs ---

public sealed record ConfigurationSettingDto(
    Guid Id,
    Guid DeployableUnitId,
    /// <summary>Null when this row is a unit-wide default (no env override).</summary>
    Guid? EnvironmentId,
    /// <summary>Null when EnvironmentId is null; denormalized server-side for UI convenience.</summary>
    string? EnvironmentName,
    string Key,
    string? Value,
    bool IsSecret,
    string? SecretReference,
    ConfigurationValueTypeDto ValueType);

public sealed record ConfigurationSettingHistoryDto(
    Guid HistoryId,
    Guid ConfigurationSettingId,
    ConfigurationChangeKindDto ChangeKind,
    string? OldValue,
    string? OldSecretReference,
    bool? OldIsSecret,
    ConfigurationValueTypeDto? OldValueType,
    string? NewValue,
    string? NewSecretReference,
    bool? NewIsSecret,
    ConfigurationValueTypeDto? NewValueType,
    string ChangedByPrincipal,
    DateTimeOffset ChangedAtUtc);

// --- Write-side requests ---

/// <summary>
/// Create a new setting. The (IsSecret, Value, SecretReference) triplet must
/// satisfy the dichotomy: secret rows carry a SecretReference and no Value;
/// plain rows carry a Value and no SecretReference. The validator enforces
/// this at the boundary so the domain error stays a 409, not a 500.
/// </summary>
public sealed record CreateConfigurationSettingRequest(
    Guid DeployableUnitId,
    Guid? EnvironmentId,
    string Key,
    bool IsSecret,
    string? Value,
    string? SecretReference,
    ConfigurationValueTypeDto ValueType,
    string ChangedByPrincipal);

public sealed record UpdateConfigurationSettingRequest(
    bool IsSecret,
    string? Value,
    string? SecretReference,
    ConfigurationValueTypeDto ValueType,
    string ChangedByPrincipal);

public sealed record DeleteConfigurationSettingRequest(string ChangedByPrincipal);
