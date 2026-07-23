using Metering.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Metering.Infrastructure.Persistence.Configurations;

public sealed class UsageRecordConfiguration : IEntityTypeConfiguration<UsageRecord>
{
    public void Configure(EntityTypeBuilder<UsageRecord> b)
    {
        b.ToTable("UsageRecord");
        b.HasKey(r => r.Id);
        b.Property(r => r.Id).ValueGeneratedNever();

        b.Property(r => r.EventId).IsRequired();
        b.Property(r => r.Meter).HasConversion<int>().IsRequired();
        b.Property(r => r.MeterType).HasConversion<int>().IsRequired();
        b.Property(r => r.Quantity).IsRequired();
        b.Property(r => r.Unit).HasMaxLength(32).IsRequired();
        b.Property(r => r.Direction).HasMaxLength(32).IsRequired();
        b.Property(r => r.Feature).HasMaxLength(128).IsRequired();
        b.Property(r => r.Model).HasMaxLength(128).IsRequired();
        b.Property(r => r.Source).HasMaxLength(64).IsRequired();
        b.Property(r => r.Repository).HasMaxLength(256);
        b.Property(r => r.Service).HasMaxLength(256);
        b.Property(r => r.Environment).HasMaxLength(128);
        b.Property(r => r.CostUsd).HasPrecision(18, 6).IsRequired();
        b.Property(r => r.RateVersion).HasMaxLength(32).IsRequired();
        b.Property(r => r.OccurredAtUtc).IsRequired();

        // Idempotent ingest: at most one row per (EventId, Direction).
        b.HasIndex(r => new { r.EventId, r.Direction }).IsUnique();
        b.HasIndex(r => r.OccurredAtUtc);
        b.HasIndex(r => new { r.Meter, r.Model });
    }
}
