using Cicd.IntegrationEvents.Ci;
using Metering.Application.Abstractions;
using Metering.Domain;
using Microsoft.Extensions.Logging;

namespace Metering.Application.Features.Ingest;

/// <summary>
/// Meters a FAILED CI pipeline run as a <see cref="MeterKind.BuildCompute"/> sample, alongside the
/// succeeded runs its sibling consumer records.
///
/// Until <c>PipelineFailed</c> existed the ledger only ever saw successful runs, which quietly made
/// build activity look like a success-only series: a week where half the runs failed metered
/// identically to a week where none did. Recording failures with the same meter and unit makes the
/// series honest; <see cref="UsageRecord.Direction"/> carries "failed" so the two can still be told
/// apart without a schema change.
///
/// Quantity is the number of steps that DID complete before the failure — the same "jobs executed"
/// measure the success path uses, not a constant, because a run that broke on step four did more
/// work than one that broke on step one.
/// </summary>
public sealed class PipelineFailedConsumer
{
    public async Task Handle(
        PipelineFailed evt,
        IUsageLedger ledger,
        ILogger<PipelineFailedConsumer> log,
        CancellationToken ct)
    {
        var record = new UsageRecord
        {
            Id = Guid.NewGuid(),
            EventId = evt.EventId,
            Meter = MeterKind.BuildCompute,
            MeterType = MeterType.Counter,
            Quantity = evt.CompletedSteps.Count,
            Unit = "job",
            // Reuses the direction column rather than adding a meter kind: this is the same
            // activity being metered, with an outcome, not a different thing to measure.
            Direction = "failed",
            Feature = evt.PipelineName,
            Model = string.Empty,
            Source = "jenkins-api",
            Repository = evt.RepositoryId?.ToString(),
            CostUsd = 0m,
            RateVersion = "n/a",
            OccurredAtUtc = evt.OccurredAtUtc,
        };

        var written = await ledger.AddAsync([record], ct);

        log.LogInformation(
            "Metered FAILED pipeline {Pipeline} run {RunId} ({Jobs} jobs completed before failure): {Reason}; rows={Rows}",
            evt.PipelineName, evt.RunId, evt.CompletedSteps.Count, evt.FailureReason, written);
    }
}
