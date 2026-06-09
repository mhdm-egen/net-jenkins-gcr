using Deployment.Domain.Deployments;
using Deployment.Domain.Deployments.Events;
using DeploymentRow = Deployment.Domain.Deployments.Deployment;

namespace Deployment.Domain.Tests.Deployments;

public class DeploymentTests
{
    private static readonly DateTimeOffset T0 = new(2026, 6, 4, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void New_deployment_is_Queued_and_emits_DeploymentQueued()
    {
        var d = NewDeployment();
        d.Status.Should().Be(DeploymentStatus.Queued);
        d.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<DeploymentQueued>();
    }

    [Fact]
    public void Cascade_parent_has_null_TargetId()
    {
        var parent = new DeploymentRow(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            targetId: null, parentDeploymentId: null,
            DeploymentStrategy.Direct, DeploymentTrigger.Manual,
            "ops", null, null, T0);

        parent.IsCascadeParent.Should().BeTrue();
        parent.TargetId.Should().BeNull();
    }

    [Fact]
    public void Cannot_Fail_from_Queued()
    {
        var d = NewDeployment();
        var act = () => d.Fail("nope", T0);
        act.Should().Throw<InvalidOperationException>().WithMessage("*expected Running*");
    }

    [Fact]
    public void Cannot_Cancel_from_Running()
    {
        var d = NewDeployment();
        d.Start(T0);
        var act = () => d.Cancel("rethink", T0);
        act.Should().Throw<InvalidOperationException>().WithMessage("*expected Queued*");
    }

    [Fact]
    public void Succeed_then_MarkRolledBack_only_works_once()
    {
        var d = NewDeployment();
        d.Start(T0);
        d.Succeed(T0.AddMinutes(1));

        var rb = Guid.NewGuid();
        d.MarkRolledBack(rb, T0.AddMinutes(2));
        d.Status.Should().Be(DeploymentStatus.RolledBack);
        d.RolledBackByDeploymentId.Should().Be(rb);

        var twice = () => d.MarkRolledBack(Guid.NewGuid(), T0.AddMinutes(3));
        twice.Should().Throw<InvalidOperationException>().WithMessage("*expected Succeeded*");
    }

    [Fact]
    public void DecideApproval_requires_existing_approval_id()
    {
        var d = NewDeployment();
        var act = () => d.DecideApproval(Guid.NewGuid(), ApprovalStatus.Approved, null, T0);
        act.Should().Throw<InvalidOperationException>().WithMessage("*not found*");
    }

    [Fact]
    public void Approval_is_immutable_after_first_decision()
    {
        var d = NewDeployment();
        var aid = Guid.NewGuid();
        d.RequestApproval(aid, "alice", T0);

        d.DecideApproval(aid, ApprovalStatus.Approved, "lgtm", T0);
        var again = () => d.DecideApproval(aid, ApprovalStatus.Rejected, "rethought", T0);
        again.Should().Throw<InvalidOperationException>().WithMessage("*already Approved*");
    }

    [Fact]
    public void SecretBinding_duplicate_throws()
    {
        var d = NewDeployment();
        var settingId = Guid.NewGuid();
        d.AddSecretBinding(settingId, "https://vault/x/v1", T0);

        var act = () => d.AddSecretBinding(settingId, "https://vault/x/v2", T0);
        act.Should().Throw<InvalidOperationException>().WithMessage("*already exists*");
    }

    private static DeploymentRow NewDeployment() =>
        new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            targetId: Guid.NewGuid(), parentDeploymentId: null,
            DeploymentStrategy.Direct, DeploymentTrigger.Manual,
            "ops", null, null, T0);
}
