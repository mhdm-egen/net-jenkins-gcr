using Metering.Application.Abstractions;
using Metering.Contracts.Usage;
using Metering.Domain;
using Microsoft.Extensions.Logging;

namespace Metering.Application.Features.Usage;

/// <summary>
/// Ingests one gauge sample — a point-in-time level rather than an accumulating flow.
///
/// Storage samples are deliberately <b>not costed</b>. The platform has no configured price per
/// byte, and inventing one would put a confident dollar figure on the usage page that nothing
/// backs. Rows land with <c>CostUsd = 0</c> and <c>RateVersion = "n/a"</c>, exactly like the
/// build and deploy meters, and the UI says so.
/// </summary>
public sealed class IngestGaugeHandler
{
    private readonly IUsageLedger _ledger;
    private readonly ILogger<IngestGaugeHandler> _log;

    public IngestGaugeHandler(IUsageLedger ledger, ILogger<IngestGaugeHandler> log)
    {
        _ledger = ledger;
        _log = log;
    }

    /// <summary>
    /// Returns null when the meter name is not a valid non-AI <see cref="MeterKind"/>; the endpoint
    /// turns that into a 400 rather than silently writing a row nobody can interpret.
    /// </summary>
    public async Task<IngestAckDto?> HandleAsync(IngestGaugeRequest req, CancellationToken ct)
    {
        if (!Enum.TryParse<MeterKind>(req.Meter, ignoreCase: true, out var meter)
            || meter == MeterKind.AiTokens)
        {
            return null;
        }

        var record = new UsageRecord
        {
            Id = Guid.NewGuid(),
            EventId = req.EventId,
            Meter = meter,
            MeterType = MeterType.Gauge,
            Quantity = req.Quantity,
            Unit = string.IsNullOrWhiteSpace(req.Unit) ? "count" : req.Unit,
            // A gauge has no direction. The unique index is (EventId, Direction), so leaving this
            // empty means one sample per event id — which is what a sample is.
            Direction = string.Empty,
            Feature = string.Empty,
            Model = string.Empty,
            Source = req.Source ?? string.Empty,
            Repository = req.Repository,
            Service = req.Service,
            Environment = req.Environment,
            CostUsd = 0m,
            RateVersion = "n/a",
            OccurredAtUtc = req.OccurredAtUtc,
        };

        var written = await _ledger.AddAsync([record], ct);

        _log.LogDebug("Gauge {Meter} = {Quantity} {Unit} ({Rows} rows)",
            meter, req.Quantity, record.Unit, written);

        return new IngestAckDto(req.EventId, written, 0m);
    }
}
