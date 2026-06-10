using Deployment.Domain.Configuration;
using Deployment.Domain.Configuration.Events;

namespace Deployment.Domain.Tests.Configuration;

public class ConfigurationSettingTests
{
    private static readonly DateTimeOffset T0 = new(2026, 6, 4, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void CreatePlain_emits_Created_event()
    {
        var s = ConfigurationSetting.CreatePlain(
            Guid.NewGuid(), Guid.NewGuid(), environmentId: null,
            "FeatureFlags:X", "true", ConfigurationValueType.Bool, "ops", T0);

        var evt = s.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<ConfigurationSettingChanged>().Subject;
        evt.ChangeKind.Should().Be(ConfigurationChangeKind.Created);
        evt.OldValue.Should().BeNull();
        evt.NewValue.Should().Be("true");
    }

    [Fact]
    public void CreateSecret_with_empty_reference_throws()
    {
        var act = () => ConfigurationSetting.CreateSecret(
            Guid.NewGuid(), Guid.NewGuid(), null, "ConnectionStrings:Db",
            secretReference: "", ConfigurationValueType.String, "ops", T0);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Update_noop_emits_no_event()
    {
        var s = ConfigurationSetting.CreatePlain(
            Guid.NewGuid(), Guid.NewGuid(), null, "Key", "v", ConfigurationValueType.String, "ops", T0);
        s.ClearDomainEvents();

        s.Update("v", newIsSecret: false, newSecretReference: null,
            ConfigurationValueType.String, "ops", T0);

        s.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void Update_plain_to_secret_emits_Updated_with_full_diff()
    {
        var s = ConfigurationSetting.CreatePlain(
            Guid.NewGuid(), Guid.NewGuid(), null, "Key", "v", ConfigurationValueType.String, "ops", T0);
        s.ClearDomainEvents();

        s.Update(newValue: null, newIsSecret: true,
            newSecretReference: "vault://k", ConfigurationValueType.String, "ops", T0);

        var evt = s.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<ConfigurationSettingChanged>().Subject;
        evt.ChangeKind.Should().Be(ConfigurationChangeKind.Updated);
        evt.OldValue.Should().Be("v");
        evt.OldIsSecret.Should().BeFalse();
        evt.NewIsSecret.Should().BeTrue();
        evt.NewSecretReference.Should().Be("vault://k");
    }

    [Fact]
    public void Update_secret_must_have_null_value()
    {
        var s = ConfigurationSetting.CreateSecret(
            Guid.NewGuid(), Guid.NewGuid(), null, "Key",
            "vault://k", ConfigurationValueType.String, "ops", T0);

        var act = () => s.Update(newValue: "leak", newIsSecret: true,
            newSecretReference: "vault://k", ConfigurationValueType.String, "ops", T0);
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*secret*null Value*");
    }

    [Fact]
    public void MarkForDeletion_emits_Deleted_with_old_state()
    {
        var s = ConfigurationSetting.CreatePlain(
            Guid.NewGuid(), Guid.NewGuid(), null, "Key", "v", ConfigurationValueType.String, "ops", T0);
        s.ClearDomainEvents();

        s.MarkForDeletion("ops", T0);

        var evt = s.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<ConfigurationSettingChanged>().Subject;
        evt.ChangeKind.Should().Be(ConfigurationChangeKind.Deleted);
        evt.OldValue.Should().Be("v");
        evt.NewValue.Should().BeNull();
    }
}
