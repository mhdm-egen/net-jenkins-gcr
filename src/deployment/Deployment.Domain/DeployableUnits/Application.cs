using Deployment.Domain.Common;
using Deployment.Domain.DeployableUnits.Events;

namespace Deployment.Domain.DeployableUnits;

/// <summary>
/// Aggregate root for a composition of services — a versioned bill-of-materials
/// that is itself deployable. The catalog membership (which services this app
/// is made of, version-agnostic) lives here as <see cref="Services"/>; per-release
/// BOM entries live on the corresponding <c>Release</c> in <c>ReleaseComposition</c>.
///
/// Application and Service are separate aggregates by deliberate choice (see
/// decisions §13). ApplicationService entries reference Service by id only —
/// no navigation property — so the catalog membership doesn't fan out a load.
/// </summary>
public sealed class Application : AggregateRoot<Guid>
{
    public DeployableUnit Unit { get; private set; }
    public string Description { get; private set; }

    private readonly List<ApplicationService> _services = new();
    public IReadOnlyCollection<ApplicationService> Services => _services.AsReadOnly();

    public string Name => Unit.Name;
    public bool IsActive => Unit.IsActive;
    public DateTimeOffset CreatedAtUtc => Unit.CreatedAtUtc;

    private Application()
    {
        Unit = null!;
        Description = string.Empty;
    }

    public Application(
        Guid id,
        string name,
        string description,
        DateTimeOffset registeredAtUtc)
    {
        if (id == Guid.Empty) throw new ArgumentException("Id cannot be empty.", nameof(id));
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Name cannot be empty.", nameof(name));

        Id = id;
        Unit = new DeployableUnit(id, name, UnitType.Application, registeredAtUtc);
        Description = description?.Trim() ?? string.Empty;

        RaiseEvent(new ApplicationRegistered(id, Unit.Name, Description, registeredAtUtc));
    }

    public void Rename(string newName, DateTimeOffset occurredAtUtc)
    {
        var trimmed = newName?.Trim() ?? string.Empty;
        if (string.Equals(Unit.Name, trimmed, StringComparison.Ordinal)) return;
        var oldName = Unit.Name;
        Unit.Rename(newName!);
        RaiseEvent(new ApplicationRenamed(Id, oldName, Unit.Name, occurredAtUtc));
    }

    public void ChangeDescription(string newDescription, DateTimeOffset occurredAtUtc)
    {
        var trimmed = newDescription?.Trim() ?? string.Empty;
        if (string.Equals(Description, trimmed, StringComparison.Ordinal)) return;
        Description = trimmed;
        RaiseEvent(new ApplicationDescriptionChanged(Id, Description, occurredAtUtc));
    }

    public void Deactivate(DateTimeOffset occurredAtUtc)
    {
        if (!Unit.IsActive) return;
        Unit.Deactivate();
        RaiseEvent(new ApplicationDeactivated(Id, occurredAtUtc));
    }

    public void Reactivate(DateTimeOffset occurredAtUtc)
    {
        if (Unit.IsActive) return;
        Unit.Reactivate();
        RaiseEvent(new ApplicationReactivated(Id, occurredAtUtc));
    }

    // --- Catalog membership ---

    /// <summary>
    /// Adds a service to this application's catalog. Idempotency: re-adding an
    /// existing service throws — use <see cref="UpdateMembership"/> if you mean
    /// to change role / order / optional flag.
    /// </summary>
    public void AddService(
        Guid serviceId,
        string role,
        bool isOptional,
        int deploymentOrder,
        DateTimeOffset occurredAtUtc)
    {
        if (serviceId == Guid.Empty) throw new ArgumentException("ServiceId cannot be empty.", nameof(serviceId));
        if (_services.Any(m => m.ServiceId == serviceId))
            throw new InvalidOperationException(
                $"Service {serviceId} is already a member of application {Id}. " +
                "Use UpdateMembership to change role/order.");

        var member = new ApplicationService(Id, serviceId, role, isOptional, deploymentOrder);
        _services.Add(member);
        RaiseEvent(new ServiceAddedToApplication(Id, serviceId, member.Role, member.IsOptional, member.DeploymentOrder, occurredAtUtc));
    }

    public void UpdateMembership(
        Guid serviceId,
        string role,
        bool isOptional,
        int deploymentOrder,
        DateTimeOffset occurredAtUtc)
    {
        var member = _services.FirstOrDefault(m => m.ServiceId == serviceId)
            ?? throw new InvalidOperationException(
                $"Service {serviceId} is not a member of application {Id}.");

        member.Update(role, isOptional, deploymentOrder);
        RaiseEvent(new ApplicationServiceMembershipUpdated(Id, serviceId, member.Role, member.IsOptional, member.DeploymentOrder, occurredAtUtc));
    }

    public void RemoveService(Guid serviceId, DateTimeOffset occurredAtUtc)
    {
        var member = _services.FirstOrDefault(m => m.ServiceId == serviceId);
        if (member is null) return;
        _services.Remove(member);
        RaiseEvent(new ServiceRemovedFromApplication(Id, serviceId, occurredAtUtc));
    }
}
