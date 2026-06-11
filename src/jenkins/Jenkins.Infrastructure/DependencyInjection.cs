using Jenkins.Application.Abstractions;
using Jenkins.Application.Features.Builds;
using Jenkins.Application.Features.Handoffs;
using Jenkins.Application.Features.Pipelines;
using Jenkins.Application.Features.PipelineRuns;
using Jenkins.Application.Features.Repositories;
using Jenkins.Client;
using Jenkins.Domain.Abstractions;
using Jenkins.Domain.Builds;
using Jenkins.Domain.Handoffs;
using Jenkins.Domain.Pipelines;
using Jenkins.Domain.PipelineRuns;
using Jenkins.Domain.SourceRepositories;
using Jenkins.Infrastructure.Sync;
using Jenkins.Infrastructure.Sync.Nexus;
using Jenkins.Infrastructure.Messaging;
using Jenkins.Infrastructure.Persistence;
using Jenkins.Infrastructure.Persistence.Readers;
using Jenkins.Infrastructure.Persistence.Repositories;
using Jenkins.Infrastructure.Releases;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Jenkins.Infrastructure;

public static class DependencyInjection
{
    /// <summary>
    /// Infrastructure DI: EF Core DbContext (SQLite) + the deployment Releases
    /// HTTP client. Repositories, projection readers, and the Jenkins build-sync
    /// adapters are added in later steps. Mirrors
    /// Deployment.Infrastructure.AddDeploymentInfrastructure.
    /// </summary>
    /// <remarks>
    /// Connection string key: <c>JenkinsCi</c>. Deployment API base URL:
    /// <c>Deployment:ApiBaseUrl</c>.
    /// </remarks>
    public static IServiceCollection AddJenkinsInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connection = configuration.GetConnectionString("JenkinsCi")
            ?? throw new InvalidOperationException(
                "ConnectionStrings:JenkinsCi is required by Jenkins.Infrastructure.");

        services.AddDbContext<JenkinsCiDbContext>(opts => opts.UseSqlServer(connection));

        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IDomainEventDispatcher, WolverineDomainEventDispatcher>();
        services.AddSingleton(TimeProvider.System);

        // Aggregate stores.
        services.AddScoped<ISourceRepositoryStore, SourceRepositoryStore>();
        services.AddScoped<IBuildStore, BuildStore>();
        services.AddScoped<IContainerReleaseHandoffStore, ContainerReleaseHandoffStore>();
        services.AddScoped<IPipelineStore, PipelineStore>();
        services.AddScoped<IPipelineRunStore, PipelineRunStore>();

        // Read-model readers.
        services.AddScoped<IRepositoryCatalogReader, EfRepositoryCatalogReader>();
        services.AddScoped<IBuildCatalogReader, EfBuildCatalogReader>();
        services.AddScoped<IHandoffReader, EfHandoffReader>();
        services.AddScoped<IPipelineReader, EfPipelineReader>();
        services.AddScoped<IPipelineRunReader, EfPipelineRunReader>();

        // The one-way handoff client. Base URL points at the deployment service.
        var deploymentBaseUrl = configuration["Deployment:ApiBaseUrl"] ?? "http://localhost:9601";
        services.AddHttpClient<IDeploymentReleaseClient, DeploymentReleaseClient>(client =>
        {
            client.BaseAddress = new Uri(deploymentBaseUrl);
        });

        return services;
    }

    /// <summary>
    /// Registers the Jenkins HTTP client and the build-sync background worker that
    /// ingests builds into the CI model. Skipped (host still runs) when Jenkins is
    /// unconfigured — set <c>Jenkins:Url</c> + <c>Jenkins:ApiToken</c> to enable.
    /// Bound from <c>"Jenkins:Sync"</c>. Mirrors Deployment.AddDeploymentRunner.
    /// </summary>
    public static IServiceCollection AddJenkinsBuildSync(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddOptions<JenkinsSyncOptions>()
            .Bind(configuration.GetSection(JenkinsSyncOptions.SectionName));

        var baseUrl = configuration["Jenkins:Url"];
        var token = configuration["Jenkins:ApiToken"];
        if (string.IsNullOrWhiteSpace(baseUrl) || string.IsNullOrWhiteSpace(token))
            return services; // Jenkins not configured — run without the sync worker

        var jenkinsOptions = new JenkinsOptions(baseUrl, configuration["Jenkins:User"] ?? "admin", token);
        services.AddSingleton(jenkinsOptions);
        services.AddSingleton<IJenkinsClient>(sp => new JenkinsClient(sp.GetRequiredService<JenkinsOptions>()));

        // Nexus artifact reconciliation (option b) — registered only when Nexus is
        // configured. Absent ⇒ the sync ingests builds without artifacts.
        var nexusOptions = configuration.GetSection(NexusReconcileOptions.SectionName).Get<NexusReconcileOptions>()
                           ?? new NexusReconcileOptions();
        if (!string.IsNullOrWhiteSpace(nexusOptions.Url) && !string.IsNullOrWhiteSpace(nexusOptions.Password))
        {
            services.AddSingleton(nexusOptions);
            services.AddSingleton<INexusArtifactReader, NexusArtifactReader>();
        }

        var enabled = configuration.GetSection(JenkinsSyncOptions.SectionName)
            .GetValue<bool?>(nameof(JenkinsSyncOptions.Enabled)) ?? true;
        if (enabled)
            services.AddHostedService<JenkinsBuildSyncService>();

        return services;
    }
}
