namespace Deployment.Domain.DeployableUnits;

/// <summary>
/// Discriminator on <see cref="DeployableUnit"/>. Tells us whether the unit is a
/// runnable thing or a manifest that bundles runnable things together.
/// </summary>
public enum UnitType
{
    Service,
    Application,
}
