using Jenkins.Domain.PipelineRuns;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Jenkins.Infrastructure.Persistence.Configurations.PipelineRuns;

/// <summary>
/// Persisted per-(run, job) console output, snapshotted from the live buffer when a run settles. A separate table
/// (not a JSON column on <c>PipelineRun</c>) because the content is large and read on demand, not with the run.
/// </summary>
public sealed class PipelineRunConsoleLogConfiguration : IEntityTypeConfiguration<PipelineRunConsoleLog>
{
    public void Configure(EntityTypeBuilder<PipelineRunConsoleLog> b)
    {
        b.ToTable("PipelineRunConsoleLog");
        b.HasKey(c => c.Id);
        b.Property(c => c.Id).ValueGeneratedNever();
        b.Property(c => c.RunId).IsRequired();
        b.Property(c => c.JobName).HasMaxLength(200).IsRequired();
        b.Property(c => c.BuildNumber).IsRequired();
        b.Property(c => c.Content).HasColumnType("nvarchar(max)").IsRequired();
        b.Property(c => c.UpdatedAtUtc).IsRequired();

        b.HasIndex(c => c.RunId);                                // read path filters by RunId
        b.HasIndex(c => new { c.RunId, c.JobName }).IsUnique();  // one row per (run, job); guards a double-settle
    }
}
