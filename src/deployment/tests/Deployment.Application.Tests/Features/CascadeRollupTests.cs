using Deployment.Application.Features.Deployments;
using Deployment.Application.Features.Deployments.FailDeployment;
using Deployment.Application.Features.Deployments.SucceedDeployment;
using Deployment.Application.Tests.Fakes;
using Deployment.Domain.Deployments;
using DeploymentRow = Deployment.Domain.Deployments.Deployment;

namespace Deployment.Application.Tests.Features;

public class CascadeRollupTests
{
    private static readonly DateTimeOffset T0 = new(2026, 6, 5, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Parent_succeeds_when_all_children_succeed()
    {
        var repo = new FakeDeploymentRepository();
        var uow = new FakeUnitOfWork();
        var clock = new FakeTimeProvider(T0);

        var parent = NewParent();
        var child1 = NewChild(parent.Id);
        var child2 = NewChild(parent.Id);
        repo.Seed(parent);
        repo.Seed(child1);
        repo.Seed(child2);

        // First child succeeds: parent should be lazy-Started but not Succeeded.
        await new SucceedDeploymentHandler(repo, uow, clock)
            .HandleAsync(new SucceedDeploymentCommand(child1.Id));
        parent.Status.Should().Be(DeploymentStatus.Running, "parent lazy-starts on first child terminal");
        child1.Status.Should().Be(DeploymentStatus.Succeeded);

        // Second child succeeds: parent should now be Succeeded.
        await new SucceedDeploymentHandler(repo, uow, clock)
            .HandleAsync(new SucceedDeploymentCommand(child2.Id));
        parent.Status.Should().Be(DeploymentStatus.Succeeded);
    }

    [Fact]
    public async Task Parent_fails_immediately_on_first_child_failure_StopAndManual()
    {
        var repo = new FakeDeploymentRepository();
        var uow = new FakeUnitOfWork();
        var clock = new FakeTimeProvider(T0);

        var parent = NewParent();
        var queuedSibling = NewQueuedChild(parent.Id); // not yet picked up by the runner
        var c1 = NewChild(parent.Id);                  // currently Running, about to fail
        repo.Seed(parent);
        repo.Seed(queuedSibling);
        repo.Seed(c1);

        await new FailDeploymentHandler(repo, uow, clock)
            .HandleAsync(new FailDeploymentCommand(c1.Id, "bad config"));

        parent.Status.Should().Be(DeploymentStatus.Failed,
            "StopAndManual semantics: parent fails as soon as any child fails");
        parent.FailureReason.Should().Contain("bad config");
        c1.Status.Should().Be(DeploymentStatus.Failed);
        queuedSibling.Status.Should().Be(DeploymentStatus.Queued,
            "Queued siblings are left alone for operator triage — domain does not auto-cancel them");
    }

    [Fact]
    public async Task Standalone_leaf_with_no_parent_does_not_blow_up()
    {
        var repo = new FakeDeploymentRepository();
        var uow = new FakeUnitOfWork();
        var clock = new FakeTimeProvider(T0);

        // ParentDeploymentId = null + TargetId set: a standalone service deploy.
        var d = new DeploymentRow(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            targetId: Guid.NewGuid(), parentDeploymentId: null,
            DeploymentStrategy.Direct, DeploymentTrigger.Manual,
            "ops", null, null, T0);
        d.Start(T0);
        repo.Seed(d);

        await new SucceedDeploymentHandler(repo, uow, clock)
            .HandleAsync(new SucceedDeploymentCommand(d.Id));

        d.Status.Should().Be(DeploymentStatus.Succeeded);
    }

    private static DeploymentRow NewParent() =>
        new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            targetId: null, parentDeploymentId: null,
            DeploymentStrategy.Direct, DeploymentTrigger.Manual,
            "ops", null, null, T0);

    private static DeploymentRow NewChild(Guid parentId)
    {
        var d = NewQueuedChild(parentId);
        d.Start(T0);
        return d;
    }

    private static DeploymentRow NewQueuedChild(Guid parentId) =>
        new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            targetId: Guid.NewGuid(), parentDeploymentId: parentId,
            DeploymentStrategy.Direct, DeploymentTrigger.Manual,
            "ops", null, null, T0);

    private sealed class FakeTimeProvider : TimeProvider
    {
        private readonly DateTimeOffset _now;
        public FakeTimeProvider(DateTimeOffset now) => _now = now;
        public override DateTimeOffset GetUtcNow() => _now;
    }
}
