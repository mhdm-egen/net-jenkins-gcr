using Deployment.Domain.Releases;
using Deployment.Domain.Releases.Events;

namespace Deployment.Domain.Tests.Releases;

public class ReleaseTests
{
    private static readonly DateTimeOffset T0 = new(2026, 6, 4, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Manifest_release_with_ArtifactUri_throws()
    {
        var act = () => new Release(Guid.NewGuid(), Guid.NewGuid(), "1.0.0", "1", "sha",
            ArtifactType.Manifest, artifactUri: "https://does/not/belong", T0);
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*ArtifactUri must be null for Manifest*");
    }

    [Fact]
    public void Service_release_without_ArtifactUri_throws()
    {
        var act = () => new Release(Guid.NewGuid(), Guid.NewGuid(), "1.0.0", "1", "sha",
            ArtifactType.ContainerImage, artifactUri: null, T0);
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*ArtifactUri is required*");
    }

    [Fact]
    public void Quarantine_without_reason_throws()
    {
        var rel = ServiceRelease();
        var act = () => rel.ChangeStatus(ReleaseStatus.Quarantined, reason: "", "ci-bot", T0);
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*reason is required*Quarantined*");
    }

    [Fact]
    public void ChangeStatus_no_op_when_same_status()
    {
        var rel = ServiceRelease();
        rel.ClearDomainEvents();
        rel.ChangeStatus(ReleaseStatus.Available, null, "ci-bot", T0);
        rel.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void ChangeStatus_to_Quarantined_emits_event_with_reason()
    {
        var rel = ServiceRelease();
        rel.ClearDomainEvents();

        rel.ChangeStatus(ReleaseStatus.Quarantined, "CVE-2026-001", "secops", T0);

        var evt = rel.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<ReleaseStatusChanged>().Subject;
        evt.FromStatus.Should().Be(ReleaseStatus.Available);
        evt.ToStatus.Should().Be(ReleaseStatus.Quarantined);
        evt.Reason.Should().Be("CVE-2026-001");
        evt.ChangedByPrincipal.Should().Be("secops");
    }

    [Fact]
    public void AttachProvenance_overwrites_and_emits_once()
    {
        var rel = ServiceRelease();
        rel.ClearDomainEvents();

        var pv = new Provenance("sha", "sbom-uri", "vuln-uri", "ci-url", "ci-id", "ci-bot");
        rel.AttachProvenance(pv, T0);
        rel.AttachProvenance(pv, T0); // same VO — no event

        rel.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<ReleaseProvenanceAttached>();
        rel.Provenance.Should().Be(pv);
    }

    private static Release ServiceRelease() =>
        new(Guid.NewGuid(), Guid.NewGuid(), "1.0.0", "100", "abc",
            ArtifactType.ContainerImage, "registry/image:1.0.0", T0);
}
