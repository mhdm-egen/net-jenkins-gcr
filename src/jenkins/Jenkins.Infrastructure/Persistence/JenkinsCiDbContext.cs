using Jenkins.Domain.Builds;
using Jenkins.Domain.Handoffs;
using Jenkins.Domain.Pipelines;
using Jenkins.Domain.PipelineRuns;
using Jenkins.Domain.SourceRepositories;
using Microsoft.EntityFrameworkCore;

namespace Jenkins.Infrastructure.Persistence;

/// <summary>
/// EF Core context for the Jenkins CI service. One DbSet per aggregate root;
/// children (e.g. DeployableComponent, BuildArtifact, ArtifactPublication) are
/// reached through their roots. Configurations under
/// <c>Persistence/Configurations/</c> are applied via
/// <see cref="ModelBuilder.ApplyConfigurationsFromAssembly"/>.
///
/// The ContainerReleaseHandoff aggregate joins the model with its own
/// configuration in step 5 of the implementation order.
/// </summary>
public sealed class JenkinsCiDbContext : DbContext
{
    public JenkinsCiDbContext(DbContextOptions<JenkinsCiDbContext> options) : base(options)
    {
    }

    public DbSet<SourceRepository> Repositories => Set<SourceRepository>();
    public DbSet<DeployableComponent> DeployableComponents => Set<DeployableComponent>();

    public DbSet<Build> Builds => Set<Build>();
    public DbSet<BuildArtifact> BuildArtifacts => Set<BuildArtifact>();
    public DbSet<ArtifactPublication> ArtifactPublications => Set<ArtifactPublication>();

    public DbSet<ContainerReleaseHandoff> Handoffs => Set<ContainerReleaseHandoff>();

    public DbSet<Pipeline> Pipelines => Set<Pipeline>();
    public DbSet<PipelineStage> PipelineStages => Set<PipelineStage>();

    public DbSet<PipelineRun> PipelineRuns => Set<PipelineRun>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(JenkinsCiDbContext).Assembly);
    }
}
