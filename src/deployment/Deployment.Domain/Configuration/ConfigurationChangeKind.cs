namespace Deployment.Domain.Configuration;

/// <summary>
/// Kind of change captured in a <c>ConfigurationSettingChanged</c> event.
/// Drives the <c>ConfigurationSettingHistory</c> projection (decisions §4.2).
/// </summary>
public enum ConfigurationChangeKind
{
    Created = 0,
    Updated = 1,
    Deleted = 2,
}
