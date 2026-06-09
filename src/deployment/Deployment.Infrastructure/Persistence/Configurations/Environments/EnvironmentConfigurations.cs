using Deployment.Domain.Environments;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Environment = Deployment.Domain.Environments.Environment;

namespace Deployment.Infrastructure.Persistence.Configurations.Environments;

public sealed class EnvironmentConfiguration : IEntityTypeConfiguration<Environment>
{
    public void Configure(EntityTypeBuilder<Environment> b)
    {
        b.ToTable("Environment");
        b.HasKey(e => e.Id);
        b.Property(e => e.Id).ValueGeneratedNever();
        b.Property(e => e.Name).HasMaxLength(100).IsRequired();
        b.Property(e => e.PromotionRank).IsRequired();
        b.Property(e => e.RequiresApproval).IsRequired();
        b.Property(e => e.IsProduction).IsRequired();

        b.HasIndex(e => e.Name).IsUnique();
        b.HasIndex(e => e.PromotionRank);

        b.HasMany(e => e.Targets)
            .WithOne()
            .HasForeignKey(t => t.EnvironmentId)
            .OnDelete(DeleteBehavior.Cascade);
        b.Navigation(e => e.Targets).AutoInclude();

        b.HasMany(e => e.FreezeWindows)
            .WithOne()
            .HasForeignKey(w => w.EnvironmentId)
            .OnDelete(DeleteBehavior.Cascade);
        b.Navigation(e => e.FreezeWindows).AutoInclude();
    }
}

public sealed class DeploymentTargetConfiguration : IEntityTypeConfiguration<DeploymentTarget>
{
    public void Configure(EntityTypeBuilder<DeploymentTarget> b)
    {
        b.ToTable("DeploymentTarget");
        b.HasKey(t => t.Id);
        b.Property(t => t.Id).ValueGeneratedNever();
        b.Property(t => t.TargetKind).HasConversion<int>().IsRequired();
        b.Property(t => t.ResourceId).HasMaxLength(500).IsRequired();
        b.Property(t => t.Region).HasMaxLength(50).IsRequired();
        b.Property(t => t.Slot).HasMaxLength(100);

        b.HasIndex(t => new { t.EnvironmentId, t.Region });
    }
}

public sealed class EnvironmentFreezeWindowConfiguration : IEntityTypeConfiguration<EnvironmentFreezeWindow>
{
    public void Configure(EntityTypeBuilder<EnvironmentFreezeWindow> b)
    {
        b.ToTable("EnvironmentFreezeWindow");
        b.HasKey(w => w.Id);
        b.Property(w => w.Id).ValueGeneratedNever();
        b.Property(w => w.StartUtc).IsRequired();
        b.Property(w => w.EndUtc).IsRequired();
        b.Property(w => w.Reason).HasMaxLength(500).IsRequired();
        b.Property(w => w.CreatedByPrincipal).HasMaxLength(200).IsRequired();
        b.Property(w => w.CreatedAtUtc).IsRequired();

        b.HasIndex(w => new { w.EnvironmentId, w.StartUtc, w.EndUtc });
    }
}
