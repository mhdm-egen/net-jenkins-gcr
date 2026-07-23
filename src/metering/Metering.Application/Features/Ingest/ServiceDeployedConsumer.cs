using Cicd.IntegrationEvents.Deployment;
using Metering.Application.Abstractions;
using Metering.Domain;
using Microsoft.Extensions.Logging;

namespace Metering.Application.Features.Ingest;

/// <summary>
/// Meters a successful service deploy (deployment.events) as a <see cref="MeterKind.DeployRun"/>
/// count, attributed to the service + environment. Idempotent on the event's EventId.
/// </summary>
public sealed class ServiceDeployedConsumer
{
    public async Task Handle(
        ServiceDeployed evt,
        IUsageLedger ledger,
        ILogger<ServiceDeployedConsumer> log,
        CancellationToken ct)
    {
        var record = new UsageRecord
        {
            Id = Guid.NewGuid(),
            EventId = evt.EventId,
            Meter = MeterKind.DeployRun,
            MeterType = MeterType.Counter,
            Quantity = 1,
            Unit = "run",
            Direction = string.Empty,
            Feature = evt.ServiceName,
            Model = string.Empty,
            Source = "deployment-api",
            Service = evt.ServiceName,
            Environment = evt.EnvironmentId.ToString(),
            CostUsd = 0m,
            RateVersion = "n/a",
            OccurredAtUtc = evt.OccurredAtUtc,
        };

        var written = await ledger.AddAsync(new[] { record }, ct);
        log.LogInformation("Metered deploy of {Service} v{Version} to env {Env} (region {Region}); rows={Rows}",
            evt.ServiceName, evt.Version, evt.EnvironmentId, evt.Region, written);
    }
}
