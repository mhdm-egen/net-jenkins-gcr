using FluentValidation;
using Jenkins.Application.Features.Builds;
using Jenkins.Application.Features.Repositories;
using Microsoft.Extensions.DependencyInjection;

namespace Jenkins.Application;

public static class DependencyInjection
{
    /// <summary>
    /// Application layer: FluentValidation registrations + handlers (registered
    /// explicitly so the list reads as a catalog of capabilities). Wolverine
    /// handler discovery is wired in the host's Program.cs. Mirrors
    /// Deployment.Application.AddDeploymentApplication.
    /// </summary>
    public static IServiceCollection AddJenkinsApplication(this IServiceCollection services)
    {
        // Validators in Features/* are picked up here as they're added.
        services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly, includeInternalTypes: true);

        // Repositories (SourceRepository) handlers.
        services.AddScoped<RegisterRepositoryHandler>();
        services.AddScoped<MapComponentHandler>();
        services.AddScoped<ListRepositoriesHandler>();
        services.AddScoped<GetRepositoryByIdHandler>();

        // Build handlers.
        services.AddScoped<RecordBuildHandler>();
        services.AddScoped<CompleteBuildHandler>();
        services.AddScoped<RecordArtifactHandler>();
        services.AddScoped<ListBuildsHandler>();
        services.AddScoped<GetBuildByIdHandler>();

        return services;
    }
}
