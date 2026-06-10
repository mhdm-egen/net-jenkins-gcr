namespace Deployment.Domain.Configuration;

/// <summary>
/// Type hint for a <see cref="ConfigurationSetting"/>'s value. Drives
/// rendering/parsing in callers; the value itself is stored as a string.
/// </summary>
public enum ConfigurationValueType
{
    String = 0,
    Int = 1,
    Bool = 2,
    Json = 3,
}
