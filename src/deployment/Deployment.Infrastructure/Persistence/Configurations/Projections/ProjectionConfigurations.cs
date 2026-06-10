using Deployment.Infrastructure.Persistence.Projections;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Deployment.Infrastructure.Persistence.Configurations.Projections;

public sealed class ConfigurationSettingHistoryConfiguration : IEntityTypeConfiguration<ConfigurationSettingHistoryRow>
{
    public void Configure(EntityTypeBuilder<ConfigurationSettingHistoryRow> b)
    {
        b.ToTable("ConfigurationSettingHistory");
        b.HasKey(h => h.HistoryId);
        b.Property(h => h.HistoryId).ValueGeneratedNever();
        b.Property(h => h.ChangeKind).HasConversion<int>().IsRequired();
        b.Property(h => h.OldValue).HasMaxLength(4000);
        b.Property(h => h.OldSecretReference).HasMaxLength(500);
        b.Property(h => h.OldIsSecret);
        b.Property(h => h.OldValueType).HasConversion<int?>();
        b.Property(h => h.NewValue).HasMaxLength(4000);
        b.Property(h => h.NewSecretReference).HasMaxLength(500);
        b.Property(h => h.NewIsSecret);
        b.Property(h => h.NewValueType).HasConversion<int?>();
        b.Property(h => h.ChangedByPrincipal).HasMaxLength(200).IsRequired();
        b.Property(h => h.ChangedAtUtc).IsRequired();

        b.HasIndex(h => new { h.ConfigurationSettingId, h.ChangedAtUtc })
            .IsDescending(false, true);
    }
}

public sealed class ReleaseStatusChangeConfiguration : IEntityTypeConfiguration<ReleaseStatusChangeRow>
{
    public void Configure(EntityTypeBuilder<ReleaseStatusChangeRow> b)
    {
        b.ToTable("ReleaseStatusChange");
        b.HasKey(c => c.ChangeId);
        b.Property(c => c.ChangeId).ValueGeneratedNever();
        b.Property(c => c.FromStatus).HasConversion<int>().IsRequired();
        b.Property(c => c.ToStatus).HasConversion<int>().IsRequired();
        b.Property(c => c.Reason).HasMaxLength(500);
        b.Property(c => c.ChangedByPrincipal).HasMaxLength(200).IsRequired();
        b.Property(c => c.ChangedAtUtc).IsRequired();

        b.HasIndex(c => new { c.ReleaseId, c.ChangedAtUtc })
            .IsDescending(false, true);
    }
}
