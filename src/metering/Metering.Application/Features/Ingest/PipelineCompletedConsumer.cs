using Cicd.IntegrationEvents.Ci;
using Metering.Application.Abstractions;
using Metering.Domain;
using Microsoft.Extensions.Logging;

namespace Metering.Application.Features.Ingest;

/// <summary>
/// Meters a completed CI pipeline run (ci.events) as a <see cref="MeterKind.BuildCompute"/>
/// sample — the number of jobs the run executed. The events carry no duration, so this is a
/// count/activity meter, not compute-seconds. Idempotent on the event's EventId.
/// </summary>
public sealed class PipelineCompletedConsumer
{
    public async Task Handle(
        PipelineCompleted evt,
        IUsageLedger ledger,
        ILogger<PipelineCompletedConsumer> log,
        CancellationToken ct)
    {
        var record = new UsageRecord
        {
            Id = Guid.NewGuid(),
            EventId = evt.EventId,
            Meter = MeterKind.BuildCompute,
            MeterType = MeterType.Counter,
            Quantity = evt.Steps.Count,
            Unit = "job",
            Direction = string.Empty,
            Feature = evt.PipelineName,
            Model = string.Empty,
            Source = "jenkins-api",
            Repository = evt.RepositoryId?.ToString(),
            CostUsd = 0m,
            RateVersion = "n/a",
            OccurredAtUtc = evt.OccurredAtUtc,
        };

        var written = await ledger.AddAsync(new[] { record }, ct);
        log.LogInformation("Metered pipeline {Pipeline} run {RunId} ({Jobs} jobs); rows={Rows}",
            evt.PipelineName, evt.RunId, evt.Steps.Count, written);
    }
}
