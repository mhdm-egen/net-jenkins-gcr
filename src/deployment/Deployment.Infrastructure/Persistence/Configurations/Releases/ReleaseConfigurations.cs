using Deployment.Domain.Releases;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Deployment.Infrastructure.Persistence.Configurations.Releases;

public sealed class ReleaseConfiguration : IEntityTypeConfiguration<Release>
{
    public void Configure(EntityTypeBuilder<Release> b)
    {
        b.ToTable("Release");
        b.HasKey(r => r.Id);
        b.Property(r => r.Id).ValueGeneratedNever();
        b.Property(r => r.DeployableUnitId).IsRequired();
        b.Property(r => r.SemanticVersion).HasMaxLength(64).IsRequired();
        b.Property(r => r.BuildNumber).HasMaxLength(64).IsRequired();
        b.Property(r => r.CommitSha).HasMaxLength(64).IsRequired();
        b.Property(r => r.ArtifactType).HasConversion<int>().IsRequired();
        b.Property(r => r.ArtifactUri).HasMaxLength(500);
        b.Property(r => r.CreatedAtUtc).IsRequired();
        b.Property(r => r.Status).HasConversion<int>().IsRequired();

        // Provenance — value object stored as columns on the same row.
        // Each field is nullable because the whole VO is nullable; the publish
        // pipeline writes them together (decisions §9.1).
        b.OwnsOne(r => r.Provenance, p =>
        {
            p.Property(x => x.ArtifactSha256).HasColumnName("ArtifactSha256").HasMaxLength(128);
            p.Property(x => x.SbomUri).HasColumnName("SbomUri").HasMaxLength(500);
            p.Property(x => x.VulnerabilityReportUri).HasColumnName("VulnerabilityReportUri").HasMaxLength(500);
            p.Property(x => x.CiRunUrl).HasColumnName("CiRunUrl").HasMaxLength(500);
            p.Property(x => x.CiRunId).HasColumnName("CiRunId").HasMaxLength(200);
            p.Property(x => x.PublishedByPrincipal).HasColumnName("PublishedByPrincipal").HasMaxLength(200);
        });

        b.HasMany(r => r.Compositions)
            .WithOne()
            .HasForeignKey(c => c.ApplicationReleaseId)
            .OnDelete(DeleteBehavior.Cascade);
        b.Navigation(r => r.Compositions).AutoInclude();

        b.HasIndex(r => new { r.DeployableUnitId, r.SemanticVersion }).IsUnique();
        b.HasIndex(r => new { r.DeployableUnitId, r.Status });
    }
}

public sealed class ReleaseCompositionConfiguration : IEntityTypeConfiguration<ReleaseComposition>
{
    public void Configure(EntityTypeBuilder<ReleaseComposition> b)
    {
        b.ToTable("ReleaseComposition", t =>
        {
            // CHECK constraint enforces the pin-mode invariant at the DB layer too
            // (handoff §3 / decisions §11).
            t.HasCheckConstraint("CK_ReleaseComposition_PinMode",
                "([PinMode] = 0 AND [ServiceReleaseId] IS NOT NULL) " +
                "OR ([PinMode] IN (1,2) AND [ServiceReleaseId] IS NULL)");
        });
        b.HasKey(c => new { c.ApplicationReleaseId, c.ServiceId });
        b.Property(c => c.PinMode).HasConversion<int>().IsRequired();
        b.Property(c => c.ServiceReleaseId);

        b.HasIndex(c => c.ServiceId);
        b.HasIndex(c => c.ServiceReleaseId);
    }
}
