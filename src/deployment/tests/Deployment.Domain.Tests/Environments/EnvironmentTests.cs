using Deployment.Domain.Environments;
using Environment = Deployment.Domain.Environments.Environment;

namespace Deployment.Domain.Tests.Environments;

public class EnvironmentTests
{
    private static readonly DateTimeOffset T0 = new(2026, 6, 4, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Schedule_then_cancel_freeze_window_round_trips()
    {
        var env = new Environment(Guid.NewGuid(), "Prod", 4, requiresApproval: true, isProduction: true, T0);
        var wId = Guid.NewGuid();

        env.ScheduleFreezeWindow(wId, T0.AddHours(1), T0.AddHours(48), "Holiday", "ops", T0);
        env.FreezeWindows.Should().ContainSingle();
        env.IsFrozenAt(T0.AddHours(2)).Should().BeTrue();
        env.IsFrozenAt(T0.AddHours(50)).Should().BeFalse();

        env.CancelFreezeWindow(wId, T0);
        env.FreezeWindows.Should().BeEmpty();
    }

    [Fact]
    public void FreezeWindow_end_before_start_throws()
    {
        var env = new Environment(Guid.NewGuid(), "Prod", 4, true, true, T0);
        var act = () => env.ScheduleFreezeWindow(Guid.NewGuid(),
            startUtc: T0.AddHours(5), endUtc: T0, "x", "ops", T0);
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*EndUtc must be after StartUtc*");
    }

    [Fact]
    public void Negative_PromotionRank_rejected()
    {
        var act = () => new Environment(Guid.NewGuid(), "x", promotionRank: -1, false, false, T0);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Add_and_update_target_persists_changes()
    {
        var env = new Environment(Guid.NewGuid(), "Dev", 1, false, false, T0);
        var tId = Guid.NewGuid();

        env.AddTarget(tId, TargetKind.KubernetesCluster, "default", "us-east", slot: null, T0);
        env.Targets.Should().ContainSingle().Which.Region.Should().Be("us-east");

        env.UpdateTarget(tId, TargetKind.KubernetesCluster, "default", "us-west", slot: null, T0);
        env.Targets.Single().Region.Should().Be("us-west");
    }
}
