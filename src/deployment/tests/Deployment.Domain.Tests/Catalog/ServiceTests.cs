using Deployment.Domain.DeployableUnits;
using Deployment.Domain.DeployableUnits.Events;

namespace Deployment.Domain.Tests.Catalog;

public class ServiceTests
{
    private static readonly DateTimeOffset T0 = new(2026, 6, 4, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Register_emits_ServiceRegistered_and_seeds_unit()
    {
        var id = Guid.NewGuid();
        var sut = new Service(id, "billing-api", ServiceKind.WebApi,
            "https://repo/example", "net10.0", T0);

        sut.Id.Should().Be(id);
        sut.Unit.Id.Should().Be(id, "Service and DeployableUnit share PK");
        sut.Unit.UnitType.Should().Be(UnitType.Service);
        sut.Name.Should().Be("billing-api");
        sut.IsActive.Should().BeTrue();
        sut.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<ServiceRegistered>()
            .Which.Name.Should().Be("billing-api");
    }

    [Fact]
    public void Rename_is_idempotent_when_unchanged()
    {
        var sut = NewService();
        sut.ClearDomainEvents();

        sut.Rename("billing-api", T0);

        sut.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void Rename_emits_event_and_updates_unit()
    {
        var sut = NewService();
        sut.ClearDomainEvents();

        sut.Rename("orders-api", T0);

        sut.Name.Should().Be("orders-api");
        sut.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<ServiceRenamed>();
    }

    [Fact]
    public void UpdateRepositoryInfo_no_op_when_unchanged()
    {
        var sut = NewService();
        sut.ClearDomainEvents();

        sut.UpdateRepositoryInfo("https://repo/example", "net10.0", T0);

        sut.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void Deactivate_then_Reactivate_round_trips_state_and_events()
    {
        var sut = NewService();
        sut.ClearDomainEvents();

        sut.Deactivate(T0);
        sut.IsActive.Should().BeFalse();
        sut.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<ServiceDeactivated>();

        sut.ClearDomainEvents();
        sut.Reactivate(T0);
        sut.IsActive.Should().BeTrue();
        sut.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<ServiceReactivated>();
    }

    [Fact]
    public void Constructor_rejects_empty_name()
    {
        var act = () => new Service(Guid.NewGuid(), "", ServiceKind.WebApi, "url", "net10.0", T0);
        act.Should().Throw<ArgumentException>();
    }

    private static Service NewService() =>
        new(Guid.NewGuid(), "billing-api", ServiceKind.WebApi,
            "https://repo/example", "net10.0", T0);
}
