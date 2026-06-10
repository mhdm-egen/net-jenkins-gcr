using System.Text.Json;
using Jenkins.Domain.Pipelines;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Jenkins.Infrastructure.Persistence.Configurations.Pipelines;

/// <summary>
/// A user-managed orchestrator pipeline + its ordered stages. Stage parameters are
/// stored as a JSON text column (the same backing-field + value-converter pattern as
/// ArtifactPublication.Tags).
/// </summary>
public sealed class PipelineConfiguration : IEntityTypeConfiguration<Pipeline>
{
    public void Configure(EntityTypeBuilder<Pipeline> b)
    {
        b.ToTable("Pipeline");
        b.HasKey(p => p.Id);
        b.Property(p => p.Id).ValueGeneratedNever();
        b.Property(p => p.Name).HasMaxLength(200).IsRequired();
        b.Property(p => p.Description).HasMaxLength(1000);
        b.Property(p => p.IsActive).IsRequired();
        b.Property(p => p.CreatedAtUtc).IsRequired();
        b.HasIndex(p => p.Name).IsUnique();

        b.Ignore(p => p.Stages); // computed ordered view; EF maps the backing field below

        b.HasMany<PipelineStage>("_stages")
            .WithOne()
            .HasForeignKey(s => s.PipelineId)
            .OnDelete(DeleteBehavior.Cascade);
        b.Navigation("_stages").AutoInclude();
    }
}

public sealed class PipelineStageConfiguration : IEntityTypeConfiguration<PipelineStage>
{
    public void Configure(EntityTypeBuilder<PipelineStage> b)
    {
        b.ToTable("PipelineStage");
        b.HasKey(s => s.Id);
        b.Property(s => s.Id).ValueGeneratedNever();
        b.Property(s => s.PipelineId).IsRequired();
        b.Property(s => s.Order).IsRequired();
        b.Property(s => s.JobName).HasMaxLength(200).IsRequired();
        b.Property(s => s.UpstreamJobName).HasMaxLength(200);

        // Parameters: backing field stored as a JSON text column.
        b.Ignore(s => s.Parameters);
        var comparer = new ValueComparer<Dictionary<string, string>>(
            (a, c) => (a == null && c == null)
                || (a != null && c != null && a.Count == c.Count
                    && a.OrderBy(k => k.Key).SequenceEqual(c.OrderBy(k => k.Key))),
            v => v.Aggregate(0, (h, kv) => HashCode.Combine(h, kv.Key.GetHashCode(), kv.Value.GetHashCode())),
            v => new Dictionary<string, string>(v));
        b.Property<Dictionary<string, string>>("_parameters")
            .HasColumnName("Parameters")
            .HasConversion(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => string.IsNullOrEmpty(v)
                    ? new Dictionary<string, string>()
                    : JsonSerializer.Deserialize<Dictionary<string, string>>(v, (JsonSerializerOptions?)null) ?? new Dictionary<string, string>(),
                comparer);

        b.HasIndex(s => new { s.PipelineId, s.Order });
    }
}
