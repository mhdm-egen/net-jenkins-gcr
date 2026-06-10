using Deployment.Domain.Configuration;
using Deployment.Domain.DeployableUnits;
using Deployment.Domain.Deployments;
using Deployment.Domain.Environments;
using Deployment.Domain.Releases;
using Deployment.Infrastructure.Persistence.Projections;
using Microsoft.EntityFrameworkCore;
using Environment = Deployment.Domain.Environments.Environment;
using DeploymentRow = Deployment.Domain.Deployments.Deployment;
using DeployableApplication = Deployment.Domain.DeployableUnits.Application;

namespace Deployment.Infrastructure.Persistence;

/// <summary>
/// EF Core context for the deployment model. Concrete
/// <c>IEntityTypeConfiguration&lt;T&gt;</c>s under <c>Persistence/Configurations/</c>
/// are picked up via <see cref="ModelBuilder.ApplyConfigurationsFromAssembly"/>.
/// Projection (read-model) tables are first-class DbSets too so EF migrations
/// create them in the same scope as the write-side aggregates.
/// </summary>
public sealed class DeploymentDbContext : DbContext
{
    public DeploymentDbContext(DbContextOptions<DeploymentDbContext> options) : base(options) { }

    // Catalog
    public DbSet<DeployableUnit> DeployableUnits => Set<DeployableUnit>();
    public DbSet<Service> Services => Set<Service>();
    public DbSet<DeployableApplication> Applications => Set<DeployableApplication>();
    public DbSet<ApplicationService> ApplicationServices => Set<ApplicationService>();

    // Releases
    public DbSet<Release> Releases => Set<Release>();
    public DbSet<ReleaseComposition> ReleaseCompositions => Set<ReleaseComposition>();

    // Environments
    public DbSet<Environment> Environments => Set<Environment>();
    public DbSet<DeploymentTarget> DeploymentTargets => Set<DeploymentTarget>();
    public DbSet<EnvironmentFreezeWindow> EnvironmentFreezeWindows => Set<EnvironmentFreezeWindow>();

    // Configuration
    public DbSet<ConfigurationSetting> ConfigurationSettings => Set<ConfigurationSetting>();

    // Deployments
    public DbSet<DeploymentRow> Deployments => Set<DeploymentRow>();
    public DbSet<Approval> Approvals => Set<Approval>();
    public DbSet<DeploymentEvent> DeploymentEvents => Set<DeploymentEvent>();
    public DbSet<DeploymentSecretBinding> DeploymentSecretBindings => Set<DeploymentSecretBinding>();

    // Projections (read-model history tables)
    public DbSet<ConfigurationSettingHistoryRow> ConfigurationSettingHistory => Set<ConfigurationSettingHistoryRow>();
    public DbSet<ReleaseStatusChangeRow> ReleaseStatusChanges => Set<ReleaseStatusChangeRow>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(DeploymentDbContext).Assembly);
    }
}
