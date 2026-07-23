using Metering.Application.Abstractions;
using Metering.Contracts.Usage;
using Metering.Domain;
using Microsoft.EntityFrameworkCore;

namespace Metering.Infrastructure.Persistence;

/// <summary>EF Core-backed <see cref="IUsageLedger"/> over SQL Server.</summary>
public sealed class EfUsageLedger : IUsageLedger
{
    private readonly MeteringDbContext _db;

    public EfUsageLedger(MeteringDbContext db) => _db = db;

    public async Task<int> AddAsync(IReadOnlyList<UsageRecord> records, CancellationToken ct)
    {
        if (records.Count == 0) return 0;

        // A batch shares one EventId (one AI call). Skip directions already recorded so a
        // retried ingest is idempotent without relying on the unique index throwing.
        var eventId = records[0].EventId;
        var existing = await _db.UsageRecords
            .Where(r => r.EventId == eventId)
            .Select(r => r.Direction)
            .ToListAsync(ct);
        var existingDirections = existing.ToHashSet();

        var toAdd = records.Where(r => !existingDirections.Contains(r.Direction)).ToList();
        if (toAdd.Count == 0) return 0;

        _db.UsageRecords.AddRange(toAdd);
        await _db.SaveChangesAsync(ct);
        return toAdd.Count;
    }

    public async Task<UsageSummaryDto> GetSummaryAsync(DateTimeOffset? fromUtc, DateTimeOffset? toUtc, CancellationToken ct)
    {
        var q = _db.UsageRecords.Where(r => r.Meter == MeterKind.AiTokens);
        if (fromUtc is { } f) q = q.Where(r => r.OccurredAtUtc >= f);
        if (toUtc is { } t) q = q.Where(r => r.OccurredAtUtc <= t);

        // Small dev volumes: pull the projected rows and aggregate in memory (avoids EF
        // conditional-sum translation quirks). Push server-side if this grows.
        var rows = await q
            .Select(r => new Row(r.EventId, r.Direction, r.Quantity, r.Model, r.Feature, r.CostUsd))
            .ToListAsync(ct);

        static long Dir(IEnumerable<Row> rs, string d) => (long)rs.Where(r => r.Direction == d).Sum(r => r.Quantity);

        var input = Dir(rows, "input");
        var output = Dir(rows, "output");
        var cacheRead = Dir(rows, "cache_read");
        var cacheWrite = Dir(rows, "cache_write");
        var cost = rows.Sum(r => r.CostUsd);
        var callCount = rows.Select(r => r.EventId).Distinct().Count();
        var cacheHitRate = (input + cacheRead) > 0 ? (double)cacheRead / (input + cacheRead) : 0d;

        var byModel = rows
            .GroupBy(r => r.Model)
            .Select(g => new UsageByModelDto(
                g.Key,
                Dir(g, "input"), Dir(g, "output"), Dir(g, "cache_read"), Dir(g, "cache_write"),
                g.Sum(r => r.CostUsd)))
            .OrderByDescending(m => m.CostUsd)
            .ToList();

        var byFeature = rows
            .GroupBy(r => r.Feature)
            .Select(g => new UsageByFeatureDto(
                g.Key,
                g.Select(r => r.EventId).Distinct().Count(),
                Dir(g, "input"), Dir(g, "output"),
                g.Sum(r => r.CostUsd)))
            .OrderByDescending(f => f.CostUsd)
            .ToList();

        return new UsageSummaryDto(callCount, input, output, cacheRead, cacheWrite, cost, cacheHitRate, byModel, byFeature);
    }

    public async Task<IReadOnlyList<MeterTotalDto>> GetMeterTotalsAsync(DateTimeOffset? fromUtc, DateTimeOffset? toUtc, CancellationToken ct)
    {
        var q = _db.UsageRecords.AsQueryable();
        if (fromUtc is { } f) q = q.Where(r => r.OccurredAtUtc >= f);
        if (toUtc is { } t) q = q.Where(r => r.OccurredAtUtc <= t);

        var rows = await q
            .Select(r => new { r.Meter, r.Quantity, r.Unit, r.CostUsd })
            .ToListAsync(ct);

        return rows
            .GroupBy(r => r.Meter)
            .Select(g => new MeterTotalDto(
                g.Key.ToString(),
                g.Count(),
                g.Sum(r => r.Quantity),
                g.Select(r => r.Unit).FirstOrDefault() ?? string.Empty,
                g.Sum(r => r.CostUsd)))
            .OrderByDescending(m => m.Records)
            .ToList();
    }

    private readonly record struct Row(Guid EventId, string Direction, double Quantity, string Model, string Feature, decimal CostUsd);
}
