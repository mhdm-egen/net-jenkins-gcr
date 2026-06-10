using Deployment.Domain.Common;
using Deployment.Domain.DeployableUnits.Events;

namespace Deployment.Domain.DeployableUnits;

/// <summary>
/// Aggregate root for a runnable .NET unit (Web API, worker, function, …).
/// Composes a <see cref="DeployableUnit"/> for the shared identity fields
/// (Name, IsActive, CreatedAtUtc, UnitType discriminator); <c>Service</c>-specific
/// fields live directly on this type.
///
/// At the schema level <c>Service.ServiceId</c> shares its PK with
/// <c>DeployableUnit.DeployableUnitId</c> — see Infrastructure/Persistence/Configurations.
/// </summary>
public sealed class Service : AggregateRoot<Guid>
{
    public DeployableUnit Unit { get; private set; }
    public ServiceKind Kind { get; private set; }
    public string RepositoryUrl { get; private set; }
    public string TargetFramework { get; private set; }

    // Convenience accessors that delegate to the embedded unit — useful at
    // call-sites where you have a Service and don't want to dereference .Unit.
    public string Name => Unit.Name;
    public bool IsActive => Unit.IsActive;
    public DateTimeOffset CreatedAtUtc => Unit.CreatedAtUtc;

    private Service()
    {
        Unit = null!;
        RepositoryUrl = string.Empty;
        TargetFramework = string.Empty;
    }

    public Service(
        Guid id,
        string name,
        ServiceKind kind,
        string repositoryUrl,
        string targetFramework,
        DateTimeOffset registeredAtUtc)
    {
        if (id == Guid.Empty) throw new ArgumentException("Id cannot be empty.", nameof(id));
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Name cannot be empty.", nameof(name));
        if (string.IsNullOrWhiteSpace(repositoryUrl))
            throw new ArgumentException("RepositoryUrl cannot be empty.", nameof(repositoryUrl));
        if (string.IsNullOrWhiteSpace(targetFramework))
            throw new ArgumentException("TargetFramework cannot be empty.", nameof(targetFramework));

        Id = id;
        Unit = new DeployableUnit(id, name, UnitType.Service, registeredAtUtc);
        Kind = kind;
        RepositoryUrl = repositoryUrl.Trim();
        TargetFramework = targetFramework.Trim();

        RaiseEvent(new ServiceRegistered(id, Unit.Name, Kind, RepositoryUrl, TargetFramework, registeredAtUtc));
    }

    public void Rename(string newName, DateTimeOffset occurredAtUtc)
    {
        if (string.Equals(Unit.Name, newName?.Trim(), StringComparison.Ordinal)) return;
        var oldName = Unit.Name;
        Unit.Rename(newName!);
        RaiseEvent(new ServiceRenamed(Id, oldName, Unit.Name, occurredAtUtc));
    }

    public void UpdateRepositoryInfo(string repositoryUrl, string targetFramework, DateTimeOffset occurredAtUtc)
    {
        if (string.IsNullOrWhiteSpace(repositoryUrl))
            throw new ArgumentException("RepositoryUrl cannot be empty.", nameof(repositoryUrl));
        if (string.IsNullOrWhiteSpace(targetFramework))
            throw new ArgumentException("TargetFramework cannot be empty.", nameof(targetFramework));

        var changed = false;
        if (!string.Equals(RepositoryUrl, repositoryUrl.Trim(), StringComparison.Ordinal))
        {
            RepositoryUrl = repositoryUrl.Trim();
            changed = true;
        }
        if (!string.Equals(TargetFramework, targetFramework.Trim(), StringComparison.Ordinal))
        {
            TargetFramework = targetFramework.Trim();
            changed = true;
        }

        if (changed)
        {
            RaiseEvent(new ServiceRepositoryInfoUpdated(Id, RepositoryUrl, TargetFramework, occurredAtUtc));
        }
    }

    public void Deactivate(DateTimeOffset occurredAtUtc)
    {
        if (!Unit.IsActive) return;
        Unit.Deactivate();
        RaiseEvent(new ServiceDeactivated(Id, occurredAtUtc));
    }

    public void Reactivate(DateTimeOffset occurredAtUtc)
    {
        if (Unit.IsActive) return;
        Unit.Reactivate();
        RaiseEvent(new ServiceReactivated(Id, occurredAtUtc));
    }
}
