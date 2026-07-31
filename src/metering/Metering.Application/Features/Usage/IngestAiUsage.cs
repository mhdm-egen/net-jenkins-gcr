using Metering.Application.Abstractions;
using Metering.Application.Observability;
using Metering.Contracts.Usage;
using Metering.Domain;
using Microsoft.Extensions.Logging;

namespace Metering.Application.Features.Usage;

/// <summary>
/// Ingests one AI call's token usage: rates each non-zero direction, expands into
/// per-direction ledger rows, and persists them idempotently (dedupe on EventId).
/// </summary>
public sealed class IngestAiUsageHandler
{
    private readonly IUsageLedger _ledger;
    private readonly IUsageRater _rater;
    private readonly MeteringTelemetry _telemetry;
    private readonly ILogger<IngestAiUsageHandler> _log;

    public IngestAiUsageHandler(
        IUsageLedger ledger,
        IUsageRater rater,
        MeteringTelemetry telemetry,
        ILogger<IngestAiUsageHandler> log)
    {
        _ledger = ledger;
        _rater = rater;
        _telemetry = telemetry;
        _log = log;
    }

    public async Task<IngestAckDto> HandleAsync(IngestAiUsageRequest req, CancellationToken ct)
    {
        var records = new List<UsageRecord>(4);
        decimal totalCost = 0m;

        void AddDirection(string direction, long tokens)
        {
            if (tokens <= 0) return;
            var cost = _rater.RateAiTokens(req.Model, direction, tokens);
            totalCost += cost;
            _telemetry.RecordAiTokens(req.Model, direction, tokens, cost);
            records.Add(new UsageRecord
            {
                Id = Guid.NewGuid(),
                EventId = req.EventId,
                Meter = MeterKind.AiTokens,
                MeterType = MeterType.Counter,
                Quantity = tokens,
                Unit = "token",
                Direction = direction,
                Feature = req.Feature ?? string.Empty,
                Model = req.Model ?? string.Empty,
                Source = req.Source ?? string.Empty,
                Repository = req.Repository,
                Service = req.Service,
                Environment = req.Environment,
                CostUsd = cost,
                RateVersion = _rater.Version,
                OccurredAtUtc = req.OccurredAtUtc,
            });
        }

        AddDirection("input", req.InputTokens);
        AddDirection("output", req.OutputTokens);
        AddDirection("cache_read", req.CacheReadTokens);
        AddDirection("cache_write", req.CacheWriteTokens);

        var written = await _ledger.AddAsync(records, ct);
        _log.LogInformation("Ingested AI usage event {EventId} feature={Feature} model={Model} rows={Rows} cost={Cost:C}",
            req.EventId, req.Feature, req.Model, written, totalCost);

        return new IngestAckDto(req.EventId, written, totalCost);
    }
}
