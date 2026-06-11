using Deployment.Application.Abstractions;
using Deployment.Application.Features.Catalog.Applications;
using Deployment.Application.Features.Catalog.ContainerImages;
using Deployment.Application.Features.Catalog.Services;
using Deployment.Application.Features.Configuration.ListConfigurationSettings;
using Deployment.Application.Features.Deployments.GetDeploymentBaseline;
using Deployment.Application.Features.Deployments.ListDeployments;
using Deployment.Application.Runner;
using Deployment.Infrastructure.Runner;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Deployment.Application.Features.Deployments.GetEffectiveVersions;
using Deployment.Application.Features.Environments.ListEnvironments;
using Deployment.Application.Features.Releases.ListReleases;
using Deployment.Domain.Abstractions;
using Deployment.Domain.Configuration;
using Deployment.Domain.ContainerImages;
using Deployment.Domain.DeployableUnits;
using Deployment.Domain.Deployments;
using Deployment.Domain.Environments;
using Deployment.Domain.Releases;
using Deployment.Infrastructure.Messaging;
using Deployment.Infrastructure.Persistence;
using Deployment.Infrastructure.Persistence.Readers;
using Deployment.Infrastructure.Persistence.Repositories;
using Deployment.Infrastructure.Registry;
using Deployment.Infrastructure.Secrets;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Deployment.Infrastructure;

public static class DependencyInjection
{
    /// <summary>
    /// Infrastructure DI: EF Core DbContext, repositories, UnitOfWork, projection
    /// readers, secret resolver. The Wolverine wireup
    /// (<c>UseWolverine</c> + <c>UseEntityFrameworkCoreTransactions</c>) lives in
    /// the host's Program.cs because it needs the <c>IHostBuilder</c>.
    /// Connection string key: <c>"Deployment"</c>.
    /// </summary>
    public static IServiceCollection AddDeploymentInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connection = configuration.GetConnectionString("Deployment")
            ?? throw new InvalidOperationException(
                "ConnectionStrings:Deployment is required by Deployment.Infrastructure.");

        services.AddDbContext<DeploymentDbContext>(opts => opts.UseSqlServer(connection));

        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IDomainEventDispatcher, WolverineDomainEventDispatcher>();
        services.AddSingleton(TimeProvider.System);

        // Repositories — explicit list so the catalog reads at a glance.
        services.AddScoped<IServiceRepository, ServiceRepository>();
        services.AddScoped<IContainerImageRepository, ContainerImageRepository>();
        services.AddScoped<IApplicationRepository, ApplicationRepository>();
        services.AddScoped<IReleaseRepository, ReleaseRepository>();
        services.AddScoped<IEnvironmentRepository, EnvironmentRepository>();
        services.AddScoped<IConfigurationSettingRepository, ConfigurationSettingRepository>();
        services.AddScoped<IDeploymentRepository, DeploymentRepository>();

        // Read-model ports.
        services.AddScoped<IEffectiveVersionsReader, EfEffectiveVersionsReader>();
        services.AddScoped<IDeploymentBaselineReader, EfDeploymentBaselineReader>();
        services.AddScoped<IServiceCatalogReader, EfServiceCatalogReader>();
        services.AddScoped<IContainerImageCatalogReader, EfContainerImageCatalogReader>();
        services.AddScoped<IApplicationCatalogReader, EfApplicationCatalogReader>();
        services.AddScoped<IReleaseCatalogReader, EfReleaseCatalogReader>();
        services.AddScoped<IEnvironmentCatalogReader, EfEnvironmentCatalogReader>();
        services.AddScoped<IConfigurationCatalogReader, EfConfigurationCatalogReader>();
        services.AddScoped<IDeploymentCatalogReader, EfDeploymentCatalogReader>();

        // Runner read-port + adapters. The fallback NoOp is registered as a
        // singleton so its logger is shared; concrete adapters can be added
        // by calling services.AddSingleton<IDeploymentAdapter, MyAdapter>() in
        // host startup, one per TargetKindDto they handle.
        services.AddScoped<IDeploymentRunnerReader, EfDeploymentRunnerReader>();
        services.AddSingleton<NoOpDeploymentAdapter>();
        services.AddSingleton<IDeploymentAdapterRegistry, DeploymentAdapterRegistry>();

        // Google Cloud Run adapter — handles TargetKind.GoogleCloudRun. Auth via
        // ADC; options bound from "Deployment:GoogleCloudRun". Singleton so the
        // ServicesClient is built once and shared.
        services.AddOptions<GoogleCloudRunOptions>()
            .Bind(configuration.GetSection(GoogleCloudRunOptions.SectionName));
        // Image promoter (Nexus → GAR, digest-preserving) used by the GCP adapter
        // when PromoteFromNexus is enabled (decision #6).
        services.AddSingleton<IArtifactPromoter, CraneArtifactPromoter>();
        services.AddSingleton<IDeploymentAdapter, GoogleCloudRunDeploymentAdapter>();

        // Container-image tag→digest resolver over the Nexus Docker Registry v2 API
        // (decision #2). Options bound from "Deployment:NexusRegistry"; degrades to
        // empty/null when ApiBaseUrl is unset.
        services.AddOptions<NexusRegistryOptions>()
            .Bind(configuration.GetSection(NexusRegistryOptions.SectionName));
        services.AddHttpClient<IContainerImageResolver, NexusContainerRegistryClient>()
            .ConfigureHttpClient((sp, client) =>
            {
                var opts = sp.GetRequiredService<IOptions<NexusRegistryOptions>>().Value;
                client.Timeout = TimeSpan.FromSeconds(Math.Max(1, opts.TimeoutSeconds));
            });

        // Stub secret resolver — replace with a real adapter (Key Vault, Vault,
        // Secrets Manager) before any unit with secret config is deployed.
        services.AddScoped<ISecretResolver, NotConfiguredSecretResolver>();

        return services;
    }

    /// <summary>
    /// Registers the in-process deployment runner as a HostedService. Bound
    /// from <c>"Deployment:Runner"</c>. Call from the API host; skip from a
    /// separate worker host that owns the runner end-to-end.
    /// </summary>
    public static IServiceCollection AddDeploymentRunner(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddOptions<DeploymentRunnerOptions>()
            .Bind(configuration.GetSection(DeploymentRunnerOptions.SectionName));

        var enabled = configuration.GetSection(DeploymentRunnerOptions.SectionName)
            .GetValue<bool?>(nameof(DeploymentRunnerOptions.Enabled)) ?? true;
        if (enabled)
        {
            services.AddHostedService<DeploymentRunnerService>();
        }
        return services;
    }
}
