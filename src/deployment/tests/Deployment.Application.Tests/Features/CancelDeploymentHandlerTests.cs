using Deployment.Application.Features.Deployments.CancelDeployment;
using Deployment.Application.Tests.Fakes;
using Deployment.Domain.Deployments;
using DeploymentRow = Deployment.Domain.Deployments.Deployment;

namespace Deployment.Application.Tests.Features;

public class CancelDeploymentHandlerTests
{
    private static readonly DateTimeOffset T0 = new(2026, 6, 5, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Cancels_queued_deployment_and_persists()
    {
        var (sut, repo, uow, _) = NewSut();
        var d = NewDeployment();
        repo.Seed(d);

        await sut.HandleAsync(new CancelDeploymentCommand(d.Id, "rethink"));

        d.Status.Should().Be(DeploymentStatus.Cancelled);
        d.CancellationReason.Should().Be("rethink");
        uow.SaveCount.Should().Be(1);
    }

    [Fact]
    public async Task Cannot_cancel_running_deployment()
    {
        var (sut, repo, _, _) = NewSut();
        var d = NewDeployment();
        d.Start(T0);
        repo.Seed(d);

        var act = async () => await sut.HandleAsync(new CancelDeploymentCommand(d.Id, "too late"));

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*expected Queued*");
    }

    [Fact]
    public async Task Missing_deployment_throws()
    {
        var (sut, _, _, _) = NewSut();
        var act = async () => await sut.HandleAsync(new CancelDeploymentCommand(Guid.NewGuid(), "x"));
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*not found*");
    }

    private static (CancelDeploymentHandler sut, FakeDeploymentRepository repo,
        FakeUnitOfWork uow, FakeTimeProvider clock) NewSut()
    {
        var repo = new FakeDeploymentRepository();
        var uow = new FakeUnitOfWork();
        var clock = new FakeTimeProvider(T0);
        return (new CancelDeploymentHandler(repo, uow, clock), repo, uow, clock);
    }

    private static DeploymentRow NewDeployment() =>
        new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            targetId: Guid.NewGuid(), parentDeploymentId: null,
            DeploymentStrategy.Direct, DeploymentTrigger.Manual,
            "ops", null, null, T0);

    private sealed class FakeTimeProvider : TimeProvider
    {
        private readonly DateTimeOffset _now;
        public FakeTimeProvider(DateTimeOffset now) => _now = now;
        public override DateTimeOffset GetUtcNow() => _now;
    }
}
