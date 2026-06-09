using Deployment.Domain.Common;

namespace Deployment.Domain.DeployableUnits;

/// <summary>
/// Shared identity record carried by both <see cref="Service"/> and
/// <see cref="Application"/>. Not an aggregate root on its own — owned by
/// whichever subtype created it.
///
/// At the database level this maps to a <c>DeployableUnit</c> table with a 1:1
/// shared-PK relationship to <c>Service</c> or <c>Application</c>. The decisions
/// doc (§13) settles this as the chosen mapping over EF Core TPT inheritance.
/// </summary>
public sealed class DeployableUnit : Entity<Guid>
{
    public string Name { get; private set; }
    public UnitType UnitType { get; private set; }
    public bool IsActive { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }

    // EF Core materialization constructor.
    private DeployableUnit()
    {
        Name = string.Empty;
    }

    internal DeployableUnit(Guid id, string name, UnitType unitType, DateTimeOffset createdAtUtc)
    {
        if (id == Guid.Empty) throw new ArgumentException("Id cannot be empty.", nameof(id));
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Name cannot be empty.", nameof(name));

        Id = id;
        Name = name.Trim();
        UnitType = unitType;
        IsActive = true;
        CreatedAtUtc = createdAtUtc;
    }

    /// <summary>
    /// Renames the unit. Visible from the outside via the subtype's <c>Rename</c>
    /// method — keeping it <c>internal</c> here means callers can't bypass the
    /// aggregate root.
    /// </summary>
    internal void Rename(string newName)
    {
        if (string.IsNullOrWhiteSpace(newName))
            throw new ArgumentException("Name cannot be empty.", nameof(newName));
        Name = newName.Trim();
    }

    internal void Deactivate() => IsActive = false;
    internal void Reactivate() => IsActive = true;
}
