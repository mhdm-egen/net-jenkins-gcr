using Deployment.Domain.Releases;
using Deployment.Domain.Releases.Events;

namespace Deployment.Domain.Tests.Releases;

public class ReleaseCompositionTests
{
    private static readonly DateTimeOffset T0 = new(2026, 6, 4, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Pinned_without_ServiceReleaseId_throws()
    {
        var rel = ManifestRelease();
        var act = () => rel.AddComposition(Guid.NewGuid(), PinMode.Pinned, serviceReleaseId: null, T0);
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Pinned requires*");
    }

    [Fact]
    public void Latest_with_ServiceReleaseId_throws()
    {
        var rel = ManifestRelease();
        var act = () => rel.AddComposition(Guid.NewGuid(), PinMode.Latest, serviceReleaseId: Guid.NewGuid(), T0);
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*requires ServiceReleaseId to be null*");
    }

    [Fact]
    public void Pinned_with_ServiceReleaseId_succeeds_and_emits_event()
    {
        var rel = ManifestRelease();
        rel.ClearDomainEvents();
        var svc = Guid.NewGuid();
        var svcRel = Guid.NewGuid();

        rel.AddComposition(svc, PinMode.Pinned, svcRel, T0);

        var added = rel.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<ReleaseCompositionEntryAdded>().Subject;
        added.PinMode.Should().Be(PinMode.Pinned);
        added.ServiceReleaseId.Should().Be(svcRel);
    }

    [Fact]
    public void AddComposition_on_Service_release_throws()
    {
        var rel = ServiceRelease();
        var act = () => rel.AddComposition(Guid.NewGuid(), PinMode.Latest, null, T0);
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*only valid on Application releases*");
    }

    [Fact]
    public void Update_changes_pin_and_emits_updated()
    {
        var rel = ManifestRelease();
        var svc = Guid.NewGuid();
        rel.AddComposition(svc, PinMode.Latest, null, T0);
        rel.ClearDomainEvents();

        var fixedVer = Guid.NewGuid();
        rel.UpdateComposition(svc, PinMode.Pinned, fixedVer, T0);

        rel.Compositions.Single().PinMode.Should().Be(PinMode.Pinned);
        rel.Compositions.Single().ServiceReleaseId.Should().Be(fixedVer);
        rel.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<ReleaseCompositionEntryUpdated>();
    }

    private static Release ManifestRelease() =>
        new(Guid.NewGuid(), Guid.NewGuid(), "1.0.0", "100", "abc",
            ArtifactType.Manifest, artifactUri: null, T0);

    private static Release ServiceRelease() =>
        new(Guid.NewGuid(), Guid.NewGuid(), "1.0.0", "100", "abc",
            ArtifactType.ContainerImage, "registry/image:1.0.0", T0);
}
