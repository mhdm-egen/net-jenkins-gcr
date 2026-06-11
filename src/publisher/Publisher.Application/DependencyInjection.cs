using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Publisher.Application.Features.Channels;
using Publisher.Application.Features.Containers;

namespace Publisher.Application;

public static class DependencyInjection
{
    /// <summary>
    /// Application-layer DI: FluentValidation validators (scanned) + handlers (registered
    /// explicitly, one line each, so the list reads as a catalog of capabilities).
    /// </summary>
    public static IServiceCollection AddPublisherApplication(this IServiceCollection services)
    {
        services.AddValidatorsFromAssemblyContaining<DependencyInjectionMarker>(includeInternalTypes: true);

        // Container inventory
        services.AddScoped<RecordContainerHandler>();
        services.AddScoped<ListContainersHandler>();
        services.AddScoped<GetContainerByIdHandler>();

        // Publishable channels
        services.AddScoped<TagContainerPublishableHandler>();
        services.AddScoped<ListChannelsHandler>();
        services.AddScoped<GetChannelByNameHandler>();

        // The bus consumer (ContainerPublishedConsumer) is discovered by Wolverine from this
        // assembly — no explicit registration needed; its injected RecordContainerHandler is above.

        return services;
    }

    /// <summary>
    /// Empty marker — gives FluentValidation an assembly anchor without exposing a real type.
    /// </summary>
    internal sealed class DependencyInjectionMarker { }
}
