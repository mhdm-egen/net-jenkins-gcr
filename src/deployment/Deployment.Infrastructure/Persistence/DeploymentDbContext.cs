using Microsoft.EntityFrameworkCore;
using Deployment.Domain.AspireApps;
using Deployment.Domain.AspireApps.Runs;
using Deployment.Domain.Containers;
using Deployment.Domain.Environments;
using Deployment.Domain.Mappings;
using Deployment.Domain.Previews;
using Deployment.Domain.Runs;
using Deployment.Domain.Services;

namespace Deployment.Infrastructure.Persistence;

public sealed class DeploymentDbContext : DbContext
{
    public DeploymentDbContext(DbContextOptions<DeploymentDbContext> options) : base(options) { }

    public DbSet<Service> Services => Set<Service>();
    public DbSet<DeploymentEnvironment> Environments => Set<DeploymentEnvironment>();
    public DbSet<DeploymentMapping> Mappings => Set<DeploymentMapping>();
    public DbSet<KnownContainer> KnownContainers => Set<KnownContainer>();
    public DbSet<DeploymentRun> Runs => Set<DeploymentRun>();
    public DbSet<AspireApplication> AspireApplications => Set<AspireApplication>();
    public DbSet<AspireApplicationRun> AspireApplicationRuns => Set<AspireApplicationRun>();
    public DbSet<PreviewEnvironment> PreviewEnvironments => Set<PreviewEnvironment>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(DeploymentDbContext).Assembly);
    }
}
