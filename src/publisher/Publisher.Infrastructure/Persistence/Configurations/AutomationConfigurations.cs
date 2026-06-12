using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Publisher.Domain.Promotions;
using Publisher.Domain.Registries;
using Publisher.Domain.Rules;

namespace Publisher.Infrastructure.Persistence.Configurations;

public sealed class RemoteRegistryConfiguration : IEntityTypeConfiguration<RemoteRegistry>
{
    public void Configure(EntityTypeBuilder<RemoteRegistry> b)
    {
        b.ToTable("RemoteRegistry");
        b.HasKey(r => r.Id);
        b.Property(r => r.Id).ValueGeneratedNever();
        b.Property(r => r.Name).HasMaxLength(200).IsRequired();
        b.Property(r => r.Provider).HasConversion<int>().IsRequired();
        b.Property(r => r.RegistryHost).HasMaxLength(300).IsRequired();
        b.Property(r => r.RepositoryPath).HasMaxLength(500).IsRequired();
        b.Property(r => r.AuthMethod).HasConversion<int>().IsRequired();
        b.Property(r => r.Username).HasMaxLength(300);
        b.Property(r => r.CredentialSecretRef).HasMaxLength(500);
        b.Property(r => r.IsDefault).IsRequired();
        b.Property(r => r.Enabled).IsRequired();
        b.Property(r => r.CreatedAtUtc).IsRequired();
        b.Property(r => r.UpdatedAtUtc).IsRequired();

        b.HasIndex(r => r.Name).IsUnique();
    }
}

public sealed class AutomationRuleConfiguration : IEntityTypeConfiguration<AutomationRule>
{
    public void Configure(EntityTypeBuilder<AutomationRule> b)
    {
        b.ToTable("AutomationRule");
        b.HasKey(r => r.Id);
        b.Property(r => r.Id).ValueGeneratedNever();
        b.Property(r => r.Name).HasMaxLength(200).IsRequired();
        b.Property(r => r.Enabled).IsRequired();
        b.Property(r => r.Trigger).HasConversion<int>().IsRequired();
        b.Property(r => r.Action).HasConversion<int>().IsRequired();
        b.Property(r => r.TargetRegistryId).IsRequired();
        b.Property(r => r.RepositoryId);
        b.Property(r => r.ContainerNamePattern).HasMaxLength(300);
        b.Property(r => r.RequirePublishable).IsRequired();
        b.Property(r => r.RequiredChannelName).HasMaxLength(200);
        b.Property(r => r.CreatedAtUtc).IsRequired();
        b.Property(r => r.UpdatedAtUtc).IsRequired();

        b.HasIndex(r => r.Name).IsUnique();
        b.HasIndex(r => new { r.Trigger, r.Enabled });
        b.HasIndex(r => r.TargetRegistryId);
    }
}

public sealed class PromotionConfiguration : IEntityTypeConfiguration<Promotion>
{
    public void Configure(EntityTypeBuilder<Promotion> b)
    {
        b.ToTable("Promotion");
        b.HasKey(p => p.Id);
        b.Property(p => p.Id).ValueGeneratedNever();
        b.Property(p => p.ContainerId).IsRequired();
        b.Property(p => p.RegistryId).IsRequired();
        b.Property(p => p.RegistryName).HasMaxLength(200).IsRequired();
        b.Property(p => p.RuleId);
        b.Property(p => p.TriggeredBy).HasMaxLength(200).IsRequired();
        b.Property(p => p.SourceRef).HasMaxLength(1000).IsRequired();
        b.Property(p => p.RemoteRef).HasMaxLength(1000).IsRequired();
        b.Property(p => p.RepositoryId).IsRequired();
        b.Property(p => p.ContainerName).HasMaxLength(300).IsRequired();
        b.Property(p => p.Version).HasMaxLength(200).IsRequired();
        b.Property(p => p.Status).HasConversion<int>().IsRequired();
        b.Property(p => p.FailureReason).HasMaxLength(2000);
        b.Property(p => p.RequestedAtUtc).IsRequired();
        b.Property(p => p.CompletedAtUtc);

        b.HasIndex(p => new { p.ContainerId, p.RegistryId });
        b.HasIndex(p => p.Status);
    }
}
