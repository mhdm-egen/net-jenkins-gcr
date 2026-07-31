using Metering.Application.Abstractions;
using Metering.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Metering.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddMeteringInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connection = configuration.GetConnectionString("Metering")
            ?? throw new InvalidOperationException("ConnectionStrings:Metering is required by Metering.Infrastructure.");

        services.AddDbContext<MeteringDbContext>(opts => opts.UseSqlServer(connection));
        services.AddScoped<IUsageLedger, EfUsageLedger>();
        services.AddSingleton(TimeProvider.System);

        return services;
    }
}
