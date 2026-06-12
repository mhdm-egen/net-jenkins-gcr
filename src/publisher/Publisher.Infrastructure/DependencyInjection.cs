using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Publisher.Application.Abstractions;
using Publisher.Application.Features.Channels;
using Publisher.Application.Features.Containers;
using Publisher.Application.Features.Promotions;
using Publisher.Application.Features.Registries;
using Publisher.Application.Features.Rules;
using Publisher.Domain.Abstractions;
using Publisher.Domain.Channels;
using Publisher.Domain.Containers;
using Publisher.Domain.Promotions;
using Publisher.Domain.Registries;
using Publisher.Domain.Rules;
using Publisher.Infrastructure.Messaging;
using Publisher.Infrastructure.Persistence;
using Publisher.Infrastructure.Persistence.Readers;
using Publisher.Infrastructure.Persistence.Repositories;
using Publisher.Infrastructure.Pushing;
using Publisher.Infrastructure.Secrets;

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
        services.AddScoped<IRemoteRegistryRepository, RemoteRegistryRepository>();
        services.AddScoped<IAutomationRuleRepository, AutomationRuleRepository>();
        services.AddScoped<IPromotionRepository, PromotionRepository>();

        // Read-model ports
        services.AddScoped<IContainerInventoryReader, EfContainerInventoryReader>();
        services.AddScoped<IChannelReader, EfChannelReader>();
        services.AddScoped<IRegistryReader, EfRegistryReader>();
        services.AddScoped<IRuleReader, EfRuleReader>();
        services.AddScoped<IPromotionReader, EfPromotionReader>();

        // Image push (crane) + runtime secret resolution (env/config). The pusher is a singleton
        // (stateless, wraps a process invocation); the secret resolver reads IConfiguration.
        services.AddOptions<PublisherPushOptions>().Bind(configuration.GetSection(PublisherPushOptions.SectionName));
        services.AddSingleton<IRegistryPusher, CraneRegistryPusher>();
        services.AddScoped<ISecretResolver, EnvironmentSecretResolver>();

        return services;
    }
}
