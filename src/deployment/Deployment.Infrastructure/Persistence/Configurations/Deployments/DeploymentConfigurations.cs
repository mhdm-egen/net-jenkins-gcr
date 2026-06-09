using Deployment.Domain.Deployments;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using DeploymentRow = Deployment.Domain.Deployments.Deployment;

namespace Deployment.Infrastructure.Persistence.Configurations.Deployments;

public sealed class DeploymentConfiguration : IEntityTypeConfiguration<DeploymentRow>
{
    public void Configure(EntityTypeBuilder<DeploymentRow> b)
    {
        b.ToTable("Deployment");
        b.HasKey(d => d.Id);
        b.Property(d => d.Id).ValueGeneratedNever();
        b.Property(d => d.ReleaseId).IsRequired();
        b.Property(d => d.EnvironmentId).IsRequired();
        b.Property(d => d.TargetId);
        b.Property(d => d.ParentDeploymentId);

        b.Property(d => d.Status).HasConversion<int>().IsRequired();
        b.Property(d => d.Strategy).HasConversion<int>().IsRequired().HasDefaultValue(DeploymentStrategy.Direct);
        b.Property(d => d.Trigger).HasConversion<int>().IsRequired();
        b.Property(d => d.TriggeredByPrincipal).HasMaxLength(200).IsRequired();
        b.Property(d => d.StartedAtUtc);
        b.Property(d => d.CompletedAtUtc);

        b.Property(d => d.SkipPromotionPathReason).HasMaxLength(500);
        b.Property(d => d.OverrideFreezeReason).HasMaxLength(500);
        b.Property(d => d.FailureReason).HasMaxLength(2000);
        b.Property(d => d.CancellationReason).HasMaxLength(500);
        b.Property(d => d.RolledBackByDeploymentId);

        b.Ignore(d => d.IsCascadeParent);
        b.Ignore(d => d.IsTerminal);

        // Self-FK for cascade rows; restrict prevents accidental cascade-delete of the parent.
        b.HasOne<DeploymentRow>()
            .WithMany()
            .HasForeignKey(d => d.ParentDeploymentId)
            .OnDelete(DeleteBehavior.Restrict);

        b.HasMany(d => d.Approvals)
            .WithOne()
            .HasForeignKey(a => a.DeploymentId)
            .OnDelete(DeleteBehavior.Cascade);
        b.Navigation(d => d.Approvals).AutoInclude();

        b.HasMany(d => d.Events)
            .WithOne()
            .HasForeignKey(e => e.DeploymentId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasMany(d => d.SecretBindings)
            .WithOne()
            .HasForeignKey(sb => sb.DeploymentId)
            .OnDelete(DeleteBehavior.Cascade);

        // Q1' hot-path (decisions §10.4) and env-rollup helpers.
        b.HasIndex(d => new { d.TargetId, d.Status, d.CompletedAtUtc });
        b.HasIndex(d => new { d.EnvironmentId, d.Status, d.CompletedAtUtc });
        b.HasIndex(d => d.ReleaseId);
        b.HasIndex(d => d.ParentDeploymentId);
    }
}

public sealed class ApprovalConfiguration : IEntityTypeConfiguration<Approval>
{
    public void Configure(EntityTypeBuilder<Approval> b)
    {
        b.ToTable("Approval");
        b.HasKey(a => a.Id);
        b.Property(a => a.Id).ValueGeneratedNever();
        b.Property(a => a.ApproverPrincipal).HasMaxLength(200).IsRequired();
        b.Property(a => a.Status).HasConversion<int>().IsRequired();
        b.Property(a => a.DecidedAtUtc);
        b.Property(a => a.Comment).HasMaxLength(2000);

        b.HasIndex(a => a.DeploymentId);
    }
}

public sealed class DeploymentEventConfiguration : IEntityTypeConfiguration<DeploymentEvent>
{
    public void Configure(EntityTypeBuilder<DeploymentEvent> b)
    {
        b.ToTable("DeploymentEvent");
        b.HasKey(e => e.Id);
        b.Property(e => e.Id).ValueGeneratedNever();
        b.Property(e => e.Timestamp).IsRequired();
        b.Property(e => e.EventType).HasMaxLength(100).IsRequired();
        b.Property(e => e.Detail);

        b.HasIndex(e => new { e.DeploymentId, e.Timestamp });
    }
}

public sealed class DeploymentSecretBindingConfiguration : IEntityTypeConfiguration<DeploymentSecretBinding>
{
    public void Configure(EntityTypeBuilder<DeploymentSecretBinding> b)
    {
        b.ToTable("DeploymentSecretBinding");
        b.HasKey(sb => new { sb.DeploymentId, sb.ConfigurationSettingId });
        b.Property(sb => sb.ResolvedSecretUri).HasMaxLength(500).IsRequired();
        b.Property(sb => sb.ResolvedAtUtc).IsRequired();

        b.HasIndex(sb => sb.ConfigurationSettingId);
    }
}
