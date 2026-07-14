using System.Text.Json;
using Jenkins.Domain.PipelineRuns;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Jenkins.Infrastructure.Persistence.Configurations.PipelineRuns;

/// <summary>
/// A server-side pipeline run. The completed-step records are stored as a JSON text column
/// (same backing-field + value-converter pattern as PipelineStage.Parameters) since they're
/// always read together with the run.
/// </summary>
public sealed class PipelineRunConfiguration : IEntityTypeConfiguration<PipelineRun>
{
    public void Configure(EntityTypeBuilder<PipelineRun> b)
    {
        b.ToTable("PipelineRun");
        b.HasKey(r => r.Id);
        b.Property(r => r.Id).ValueGeneratedNever();
        b.Property(r => r.PipelineId).IsRequired();
        b.Property(r => r.PipelineName).HasMaxLength(200).IsRequired();
        b.Property(r => r.RepositoryId);
        b.Property(r => r.Branch).HasMaxLength(300);
        b.Property(r => r.TriggeredBy).HasMaxLength(200).IsRequired();
        b.Property(r => r.Status).HasConversion<int>().IsRequired();
        b.Property(r => r.StartedAtUtc).IsRequired();
        b.Property(r => r.CompletedAtUtc);
        b.Property(r => r.FailureReason).HasMaxLength(2000);

        b.Ignore(r => r.Steps); // computed ordered view; the backing field is mapped below

        var comparer = new ValueComparer<List<PipelineRunStepRecord>>(
            (a, c) => (a == null && c == null) || (a != null && c != null && a.SequenceEqual(c)),
            v => v.Aggregate(0, (h, s) => HashCode.Combine(h, s.GetHashCode())),
            v => v.ToList());
        b.Property<List<PipelineRunStepRecord>>("_steps")
            .HasColumnName("Steps")
            .HasConversion(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => string.IsNullOrEmpty(v)
                    ? new List<PipelineRunStepRecord>()
                    : JsonSerializer.Deserialize<List<PipelineRunStepRecord>>(v, (JsonSerializerOptions?)null) ?? new List<PipelineRunStepRecord>(),
                comparer);

        b.HasIndex(r => r.PipelineId);
        b.HasIndex(r => r.StartedAtUtc);
    }
}
