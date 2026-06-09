using Deployment.Application.Features.Configuration.ResolveEffectiveConfig;
using Deployment.Application.Tests.Fakes;
using Deployment.Domain.Configuration;

namespace Deployment.Application.Tests.Features;

public class ResolveEffectiveConfigHandlerTests
{
    private static readonly DateTimeOffset T0 = new(2026, 6, 4, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task App_env_beats_Service_env_beats_App_default_beats_Service_default()
    {
        var svc = Guid.NewGuid();
        var app = Guid.NewGuid();
        var env = Guid.NewGuid();
        var settings = new FakeConfigurationSettingRepository();

        Seed(settings, svc, env: null, key: "X", value: "svc-default");
        Seed(settings, svc, env: env, key: "X", value: "svc-env");
        Seed(settings, app, env: null, key: "X", value: "app-default");
        Seed(settings, app, env: env, key: "X", value: "app-env");

        var sut = new ResolveEffectiveConfigHandler(settings);
        var result = await sut.HandleAsync(new ResolveEffectiveConfigQuery(svc, app, env));

        var hit = result.Entries.Single();
        hit.Value.Should().Be("app-env");
        hit.Origin.Should().Be(ConfigOrigin.ApplicationEnvironment);
    }

    [Fact]
    public async Task Falls_through_to_lower_tier_when_higher_is_missing()
    {
        var svc = Guid.NewGuid();
        var app = Guid.NewGuid();
        var env = Guid.NewGuid();
        var settings = new FakeConfigurationSettingRepository();

        // Only svc-env exists.
        Seed(settings, svc, env: env, key: "X", value: "svc-env");

        var sut = new ResolveEffectiveConfigHandler(settings);
        var result = await sut.HandleAsync(new ResolveEffectiveConfigQuery(svc, app, env));

        var hit = result.Entries.Single();
        hit.Value.Should().Be("svc-env");
        hit.Origin.Should().Be(ConfigOrigin.ServiceEnvironment);
    }

    [Fact]
    public async Task Service_only_resolves_without_application_context()
    {
        var svc = Guid.NewGuid();
        var env = Guid.NewGuid();
        var settings = new FakeConfigurationSettingRepository();
        Seed(settings, svc, env: null, key: "K", value: "default");

        var sut = new ResolveEffectiveConfigHandler(settings);
        var result = await sut.HandleAsync(new ResolveEffectiveConfigQuery(svc, ApplicationId: null, env));

        result.Entries.Single().Origin.Should().Be(ConfigOrigin.ServiceDefault);
    }

    private static void Seed(FakeConfigurationSettingRepository repo, Guid unitId, Guid? env, string key, string value)
    {
        var s = ConfigurationSetting.CreatePlain(
            Guid.NewGuid(), unitId, env, key, value, ConfigurationValueType.String, "ops", T0);
        repo.Seed(s);
    }
}
