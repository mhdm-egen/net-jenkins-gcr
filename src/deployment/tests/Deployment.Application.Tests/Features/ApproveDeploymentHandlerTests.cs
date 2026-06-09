using Deployment.Application.Features.Deployments.ApproveDeployment;
using Deployment.Application.Tests.Fakes;
using Deployment.Contracts.Deployments;
using Deployment.Domain.Deployments;
using DeploymentRow = Deployment.Domain.Deployments.Deployment;

namespace Deployment.Application.Tests.Features;

public class ApproveDeploymentHandlerTests
{
    private static readonly DateTimeOffset T0 = new(2026, 6, 4, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Approver_equal_to_triggerer_is_rejected_with_SoD_message()
    {
        var deployments = new FakeDeploymentRepository();
        var uow = new FakeUnitOfWork();
        var clock = new FakeTimeProvider(T0);

        var d = NewDeployment("alice");
        deployments.Seed(d);

        var sut = new ApproveDeploymentHandler(deployments, uow, clock);
        var act = async () => await sut.HandleAsync(
            new ApproveDeploymentCommand(d.Id, Guid.NewGuid(), "alice", ApprovalStatusDto.Approved, null));

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Segregation-of-duties*");
        uow.SaveCount.Should().Be(0);
    }

    [Fact]
    public async Task Different_approver_opens_and_decides_in_one_call()
    {
        var deployments = new FakeDeploymentRepository();
        var uow = new FakeUnitOfWork();
        var clock = new FakeTimeProvider(T0);

        var d = NewDeployment("alice");
        deployments.Seed(d);

        var sut = new ApproveDeploymentHandler(deployments, uow, clock);
        var aId = Guid.NewGuid();
        await sut.HandleAsync(new ApproveDeploymentCommand(
            d.Id, aId, "bob", ApprovalStatusDto.Approved, "lgtm"));

        var approval = d.Approvals.Should().ContainSingle().Subject;
        approval.Status.Should().Be(ApprovalStatus.Approved);
        approval.ApproverPrincipal.Should().Be("bob");
        approval.Comment.Should().Be("lgtm");
        uow.SaveCount.Should().Be(1);
    }

    [Fact]
    public async Task Approval_belongs_to_different_principal_throws()
    {
        var deployments = new FakeDeploymentRepository();
        var uow = new FakeUnitOfWork();
        var clock = new FakeTimeProvider(T0);

        var d = NewDeployment("alice");
        var aId = Guid.NewGuid();
        d.RequestApproval(aId, "carol", T0);
        deployments.Seed(d);

        var sut = new ApproveDeploymentHandler(deployments, uow, clock);
        var act = async () => await sut.HandleAsync(
            new ApproveDeploymentCommand(d.Id, aId, "bob", ApprovalStatusDto.Approved, null));

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*belongs to a different principal*");
    }

    private static DeploymentRow NewDeployment(string triggerer) =>
        new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            targetId: Guid.NewGuid(), parentDeploymentId: null,
            DeploymentStrategy.Direct, DeploymentTrigger.Manual,
            triggerer, null, null, T0);

    private sealed class FakeTimeProvider : TimeProvider
    {
        private readonly DateTimeOffset _now;
        public FakeTimeProvider(DateTimeOffset now) => _now = now;
        public override DateTimeOffset GetUtcNow() => _now;
    }
}
