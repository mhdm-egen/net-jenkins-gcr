using Deployment.Domain.DeployableUnits;
using Deployment.Domain.DeployableUnits.Events;
using DeployableApplication = Deployment.Domain.DeployableUnits.Application;

namespace Deployment.Domain.Tests.Catalog;

public class ApplicationTests
{
    private static readonly DateTimeOffset T0 = new(2026, 6, 4, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void AddService_twice_for_same_service_throws()
    {
        var sut = NewApp();
        var svc = Guid.NewGuid();
        sut.AddService(svc, "api", isOptional: false, deploymentOrder: 10, T0);

        var act = () => sut.AddService(svc, "worker", isOptional: false, deploymentOrder: 20, T0);
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*already a member*");
    }

    [Fact]
    public void UpdateMembership_for_non_member_throws()
    {
        var sut = NewApp();
        var act = () => sut.UpdateMembership(Guid.NewGuid(), "api", false, 10, T0);
        act.Should().Throw<InvalidOperationException>().WithMessage("*not a member*");
    }

    [Fact]
    public void RemoveService_for_non_member_is_idempotent()
    {
        var sut = NewApp();
        sut.ClearDomainEvents();

        sut.RemoveService(Guid.NewGuid(), T0);

        sut.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void AddService_emits_event_with_correct_payload()
    {
        var sut = NewApp();
        var svc = Guid.NewGuid();
        sut.ClearDomainEvents();

        sut.AddService(svc, "api", true, 5, T0);

        var evt = sut.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<ServiceAddedToApplication>().Subject;
        evt.ServiceId.Should().Be(svc);
        evt.Role.Should().Be("api");
        evt.IsOptional.Should().BeTrue();
        evt.DeploymentOrder.Should().Be(5);
    }

    [Fact]
    public void UpdateMembership_changes_role_and_order_and_emits_event()
    {
        var sut = NewApp();
        var svc = Guid.NewGuid();
        sut.AddService(svc, "api", false, 10, T0);
        sut.ClearDomainEvents();

        sut.UpdateMembership(svc, "worker", true, 20, T0);

        var member = sut.Services.Single();
        member.Role.Should().Be("worker");
        member.IsOptional.Should().BeTrue();
        member.DeploymentOrder.Should().Be(20);
        sut.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<ApplicationServiceMembershipUpdated>();
    }

    private static DeployableApplication NewApp() =>
        new(Guid.NewGuid(), "checkout", "Checkout flow", T0);
}
