using Metering.Application.Abstractions;
using Metering.Application.Features.Usage;
using Metering.Application.Observability;
using Metering.Application.Rating;
using Microsoft.Extensions.DependencyInjection;

namespace Metering.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddMeteringApplication(this IServiceCollection services)
    {
        services.AddSingleton<IUsageRater, UsageRater>();
        services.AddSingleton<MeteringTelemetry>();

        services.AddScoped<IngestAiUsageHandler>();
        services.AddScoped<GetUsageSummaryHandler>();
        services.AddScoped<GetMeterTotalsHandler>();

        return services;
    }
}
