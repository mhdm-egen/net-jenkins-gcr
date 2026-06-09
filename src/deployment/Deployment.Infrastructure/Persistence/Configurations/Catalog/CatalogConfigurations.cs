using Deployment.Domain.DeployableUnits;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using DeployableApplication = Deployment.Domain.DeployableUnits.Application;

namespace Deployment.Infrastructure.Persistence.Configurations.Catalog;

/// <summary>
/// DeployableUnit owns the shared identity (Name uniqueness, IsActive, CreatedAt,
/// UnitType discriminator). Service and Application each live in their own
/// table and share the PK value with their DeployableUnit row — the "shared-PK
/// 1:1" split (decisions §13 / handoff stack confirmation).
/// </summary>
public sealed class DeployableUnitConfiguration : IEntityTypeConfiguration<DeployableUnit>
{
    public void Configure(EntityTypeBuilder<DeployableUnit> b)
    {
        b.ToTable("DeployableUnit");
        b.HasKey(u => u.Id);
        b.Property(u => u.Id).ValueGeneratedNever();
        b.Property(u => u.Name).HasMaxLength(200).IsRequired();
        b.Property(u => u.UnitType).HasConversion<int>().IsRequired();
        b.Property(u => u.IsActive).IsRequired();
        b.Property(u => u.CreatedAtUtc).IsRequired();
        b.HasIndex(u => u.Name).IsUnique();
    }
}

public sealed class ServiceConfiguration : IEntityTypeConfiguration<Service>
{
    public void Configure(EntityTypeBuilder<Service> b)
    {
        b.ToTable("Service");
        b.HasKey(s => s.Id);
        b.Property(s => s.Id).ValueGeneratedNever();
        b.Property(s => s.Kind).HasConversion<int>().IsRequired();
        b.Property(s => s.RepositoryUrl).HasMaxLength(500).IsRequired();
        b.Property(s => s.TargetFramework).HasMaxLength(50).IsRequired();

        // Shared-PK 1:1: Service.Id IS the DeployableUnit row's PK. Service is
        // the dependent (it requires the unit to exist).
        b.HasOne(s => s.Unit)
            .WithOne()
            .HasForeignKey<Service>(s => s.Id)
            .OnDelete(DeleteBehavior.Restrict);
        b.Navigation(s => s.Unit).IsRequired();

        b.Ignore(s => s.Name);
        b.Ignore(s => s.IsActive);
        b.Ignore(s => s.CreatedAtUtc);
    }
}

public sealed class ApplicationConfiguration : IEntityTypeConfiguration<DeployableApplication>
{
    public void Configure(EntityTypeBuilder<DeployableApplication> b)
    {
        b.ToTable("Application");
        b.HasKey(a => a.Id);
        b.Property(a => a.Id).ValueGeneratedNever();
        b.Property(a => a.Description).HasMaxLength(1000).IsRequired();

        b.HasOne(a => a.Unit)
            .WithOne()
            .HasForeignKey<DeployableApplication>(a => a.Id)
            .OnDelete(DeleteBehavior.Restrict);
        b.Navigation(a => a.Unit).IsRequired();

        // Catalog membership: owned-collection (single-aggregate boundary).
        b.HasMany(a => a.Services)
            .WithOne()
            .HasForeignKey(ap => ap.ApplicationId)
            .OnDelete(DeleteBehavior.Cascade);
        b.Navigation(a => a.Services).AutoInclude();

        b.Ignore(a => a.Name);
        b.Ignore(a => a.IsActive);
        b.Ignore(a => a.CreatedAtUtc);
    }
}

public sealed class ApplicationServiceConfiguration : IEntityTypeConfiguration<ApplicationService>
{
    public void Configure(EntityTypeBuilder<ApplicationService> b)
    {
        b.ToTable("ApplicationService");
        b.HasKey(aps => new { aps.ApplicationId, aps.ServiceId });
        b.Property(aps => aps.Role).HasMaxLength(100).IsRequired();
        b.Property(aps => aps.IsOptional).IsRequired();
        b.Property(aps => aps.DeploymentOrder).HasDefaultValue(0).IsRequired();

        // Reference to the Service aggregate by id only — no navigation,
        // per the deliberate "Application and Service are separate aggregates" choice.
        b.HasIndex(aps => aps.ServiceId);
    }
}
