using Jenkins.Domain.Handoffs;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Jenkins.Infrastructure.Persistence.Configurations.Handoffs;

/// <summary>
/// The integration record. <see cref="ContainerReleaseHandoff.DeploymentReleaseId"/>
/// is a value reference to the deployment service's Release — no FK across the
/// boundary. Indexes support reverse-provenance (Q1) and the promotion backlog (Q4).
/// </summary>
public sealed class ContainerReleaseHandoffConfiguration : IEntityTypeConfiguration<ContainerReleaseHandoff>
{
    public void Configure(EntityTypeBuilder<ContainerReleaseHandoff> b)
    {
        b.ToTable("ContainerReleaseHandoff");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedNever();
        b.Property(x => x.BuildId).IsRequired();
        b.Property(x => x.BuildArtifactId).IsRequired();
        b.Property(x => x.DeployableComponentId).IsRequired();
        b.Property(x => x.RepositoryId).IsRequired();
        b.Property(x => x.DeployableUnitId).IsRequired();
        b.Property(x => x.DeploymentReleaseId);
        b.Property(x => x.SemanticVersion).HasMaxLength(200).IsRequired();
        b.Property(x => x.ArtifactUri).HasMaxLength(500).IsRequired();
        b.Property(x => x.Status).HasConversion<int>().IsRequired();
        b.Property(x => x.RequestedByPrincipal).HasMaxLength(200).IsRequired();
        b.Property(x => x.CreatedAtUtc).IsRequired();
        b.Property(x => x.SettledAtUtc);
        b.Property(x => x.FailureReason).HasMaxLength(2000);

        b.HasIndex(x => x.DeploymentReleaseId);       // Q1 — reverse provenance
        b.HasIndex(x => new { x.BuildId, x.Status });  // Q4 — promotion backlog
        b.HasIndex(x => x.BuildArtifactId);
    }
}
