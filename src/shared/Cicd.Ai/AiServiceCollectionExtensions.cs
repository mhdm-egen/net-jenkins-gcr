using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Cicd.Ai;

public static class AiServiceCollectionExtensions
{
    /// <summary>
    /// Registers the whole AI layer: <see cref="AiOptions"/> and
    /// <see cref="MeteringApiOptions"/> bound from config, the two usage sinks fanned out through
    /// <see cref="CompositeAiUsageRecorder"/>, <see cref="IAiInsightService"/>, and the shared
    /// <see cref="AiExplanationRunner"/>.
    ///
    /// Deliberately does NOT fail when <c>Ai:ApiKey</c> is absent — <see cref="AiClient"/> records a
    /// configuration error and every feature checks <c>IsConfigured</c>, so an unkeyed host runs
    /// normally with its AI affordances hidden.
    ///
    /// Two things the caller still owns, because they are host-level concerns:
    /// <list type="bullet">
    /// <item>an <see cref="Microsoft.Extensions.Caching.Distributed.IDistributedCache"/> — Redis
    /// where available, otherwise <c>AddDistributedMemoryCache()</c>;</item>
    /// <item>adding <see cref="MeterAiUsageRecorder.MeterName"/> to the OpenTelemetry
    /// MeterProvider, so the <c>Cicd.Ai</c> meter is actually exported.</item>
    /// </list>
    /// </summary>
    public static IServiceCollection AddCicdAi(this IServiceCollection services, IConfiguration configuration)
    {
        var aiOptions = configuration.GetSection(AiOptions.SectionName).Get<AiOptions>() ?? new AiOptions();
        services.AddSingleton(aiOptions);

        var meteringOptions = configuration.GetSection(MeteringApiOptions.SectionName).Get<MeteringApiOptions>()
                              ?? new MeteringApiOptions();
        services.AddSingleton(meteringOptions);

        // Only register the ingest client when there's somewhere to send it; the recorder itself
        // no-ops on an empty BaseUrl either way.
        if (!string.IsNullOrWhiteSpace(meteringOptions.BaseUrl))
            services.AddHttpClient(MeteringUsageRecorder.HttpClientName, c =>
                c.BaseAddress = new Uri(meteringOptions.BaseUrl.EndsWith('/')
                    ? meteringOptions.BaseUrl
                    : meteringOptions.BaseUrl + "/"));

        services.AddSingleton<MeterAiUsageRecorder>();
        services.AddSingleton<MeteringUsageRecorder>();
        services.AddSingleton<IAiUsageRecorder>(sp => new CompositeAiUsageRecorder(
            sp.GetRequiredService<MeterAiUsageRecorder>(),
            sp.GetRequiredService<MeteringUsageRecorder>()));

        services.AddSingleton<IAiInsightService, AiClient>();
        services.AddScoped<AiExplanationRunner>();

        return services;
    }
}
