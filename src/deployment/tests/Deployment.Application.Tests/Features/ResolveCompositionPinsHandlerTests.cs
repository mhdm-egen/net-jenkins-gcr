using Deployment.Application.Features.Releases.ResolveCompositionPins;
using Deployment.Application.Tests.Fakes;
using Deployment.Domain.Deployments;
using Deployment.Domain.Releases;
using DeploymentRow = Deployment.Domain.Deployments.Deployment;

namespace Deployment.Application.Tests.Features;

public class ResolveCompositionPinsHandlerTests
{
    private static readonly DateTimeOffset T0 = new(2026, 6, 4, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Pinned_entry_passes_through_with_Pinned_reason()
    {
        var (handler, releases, _, appReleaseId) = NewSut();
        var serviceId = Guid.NewGuid();
        var pinnedRel = NewServiceRelease(serviceId);
        releases.Seed(pinnedRel);

        var app = releases.All[appReleaseId];
        app.AddComposition(serviceId, PinMode.Pinned, pinnedRel.Id, T0);

        var result = await handler.HandleAsync(
            new ResolveCompositionPinsQuery(appReleaseId, EnvironmentId: Guid.NewGuid()));

        var entry = result.Entries.Should().ContainSingle().Subject;
        entry.ServiceId.Should().Be(serviceId);
        entry.ResolvedServiceReleaseId.Should().Be(pinnedRel.Id);
        entry.Reason.Should().Be(PinResolutionReason.Pinned);
    }

    [Fact]
    public async Task Latest_picks_newest_Available()
    {
        var (handler, releases, _, appReleaseId) = NewSut();
        var serviceId = Guid.NewGuid();
        var older = NewServiceRelease(serviceId, T0.AddMinutes(-1));
        var newer = NewServiceRelease(serviceId, T0);
        releases.Seed(older);
        releases.Seed(newer);

        releases.All[appReleaseId].AddComposition(serviceId, PinMode.Latest, null, T0);

        var result = await handler.HandleAsync(
            new ResolveCompositionPinsQuery(appReleaseId, Guid.NewGuid()));

        result.Entries.Single().ResolvedServiceReleaseId.Should().Be(newer.Id);
        result.Entries.Single().Reason.Should().Be(PinResolutionReason.Latest);
    }

    [Fact]
    public async Task Current_falls_back_to_Latest_when_never_deployed()
    {
        var (handler, releases, _, appReleaseId) = NewSut();
        var serviceId = Guid.NewGuid();
        var rel = NewServiceRelease(serviceId);
        releases.Seed(rel);

        releases.All[appReleaseId].AddComposition(serviceId, PinMode.Current, null, T0);

        var result = await handler.HandleAsync(
            new ResolveCompositionPinsQuery(appReleaseId, Guid.NewGuid()));

        var entry = result.Entries.Single();
        entry.ResolvedServiceReleaseId.Should().Be(rel.Id);
        entry.Reason.Should().Be(PinResolutionReason.CurrentFellBackToLatest);
    }

    [Fact]
    public async Task Latest_throws_when_no_releases_for_service()
    {
        var (handler, _, _, appReleaseId) = NewSut();
        var serviceId = Guid.NewGuid();
        // Note: no service-release seeded.
        // Need to seed via the handler-resolved app release, since the fake repo
        // returns the same instance each call.
        var act = async () =>
        {
            await handler.HandleAsync(new ResolveCompositionPinsQuery(appReleaseId, Guid.NewGuid()));
        };

        // Without a composition, the handler succeeds with an empty list — add one
        // pointing at an unseeded service to force the failure path.
        var resolveLatestAct = async () =>
        {
            var sut = NewSut();
            sut.releases.All[sut.appReleaseId].AddComposition(serviceId, PinMode.Latest, null, T0);
            await sut.handler.HandleAsync(new ResolveCompositionPinsQuery(sut.appReleaseId, Guid.NewGuid()));
        };

        await resolveLatestAct.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*no available releases*");
    }

    [Fact]
    public async Task Current_uses_running_release_when_one_exists()
    {
        var (handler, releases, deployments, appReleaseId) = NewSut();
        var serviceId = Guid.NewGuid();
        var runningRel = NewServiceRelease(serviceId);
        var latestRel = NewServiceRelease(serviceId, T0.AddMinutes(5));
        releases.Seed(runningRel);
        releases.Seed(latestRel);
        deployments.ReleaseToUnit = id =>
        {
            if (id == runningRel.Id) return serviceId;
            if (id == latestRel.Id) return serviceId;
            return Guid.Empty;
        };

        var envId = Guid.NewGuid();
        var dep = new DeploymentRow(Guid.NewGuid(), runningRel.Id, envId,
            Guid.NewGuid(), null,
            DeploymentStrategy.Direct, DeploymentTrigger.Manual,
            "ops", null, null, T0);
        dep.Start(T0);
        dep.Succeed(T0.AddSeconds(1));
        deployments.Seed(dep);

        releases.All[appReleaseId].AddComposition(serviceId, PinMode.Current, null, T0);

        var result = await handler.HandleAsync(new ResolveCompositionPinsQuery(appReleaseId, envId));

        result.Entries.Single().ResolvedServiceReleaseId.Should().Be(runningRel.Id);
        result.Entries.Single().Reason.Should().Be(PinResolutionReason.Current);
    }

    private static Release NewServiceRelease(Guid serviceId, DateTimeOffset? createdAt = null) =>
        new(Guid.NewGuid(), serviceId,
            $"1.0.{Guid.NewGuid().GetHashCode() & 0xFFFF}", "100", "sha",
            ArtifactType.ContainerImage, "registry/img:1.0.0", createdAt ?? T0);

    private static (ResolveCompositionPinsHandler handler, FakeReleaseRepository releases,
        FakeDeploymentRepository deployments, Guid appReleaseId) NewSut()
    {
        var releases = new FakeReleaseRepository();
        var deployments = new FakeDeploymentRepository();
        var appRel = new Release(Guid.NewGuid(), Guid.NewGuid(),
            "1.0.0", "100", "sha", ArtifactType.Manifest, null, T0);
        releases.Seed(appRel);
        return (new ResolveCompositionPinsHandler(releases, deployments),
            releases, deployments, appRel.Id);
    }
}
