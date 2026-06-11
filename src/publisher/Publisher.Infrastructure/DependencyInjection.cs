using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Publisher.Application.Features.Channels;
using Publisher.Application.Features.Containers;
using Publisher.Domain.Abstractions;
using Publisher.Domain.Channels;
using Publisher.Domain.Containers;
using Publisher.Infrastructure.Messaging;
using Publisher.Infrastructure.Persistence;
using Publisher.Infrastructure.Persistence.Readers;
using Publisher.Infrastructure.Persistence.Repositories;

namespace Publisher.Infrastructure;

public static class DependencyInjection
{
    /// <summary>
    /// Infrastructure DI: EF Core DbContext, repositories, UnitOfWork, projection readers.
    /// The Wolverine wireup (<c>UseWolverine</c> + <c>UseEntityFrameworkCoreTransactions</c>)
    /// lives in the host's Program.cs because it needs the <c>IHostBuilder</c>.
    /// Connection string key: <c>"Publisher"</c>.
    /// </summary>
    public static IServiceCollection AddPublisherInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connection = configuration.GetConnectionString("Publisher")
            ?? throw new InvalidOperationException(
                "ConnectionStrings:Publisher is required by Publisher.Infrastructure.");

        services.AddDbContext<PublisherDbContext>(opts => opts.UseSqlServer(connection));

        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IDomainEventDispatcher, WolverineDomainEventDispatcher>();
        services.AddSingleton(TimeProvider.System);

        // Repositories
        services.AddScoped<IPublishableContainerRepository, PublishableContainerRepository>();
        services.AddScoped<IPublishChannelRepository, PublishChannelRepository>();

        // Read-model ports
        services.AddScoped<IContainerInventoryReader, EfContainerInventoryReader>();
        services.AddScoped<IChannelReader, EfChannelReader>();

        return services;
    }
}
