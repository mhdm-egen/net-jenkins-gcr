using Metering.Contracts.Usage;
using Metering.Domain;

namespace Metering.Application.Abstractions;

/// <summary>The persistent, rated usage ledger. Implemented in Infrastructure over EF Core.</summary>
public interface IUsageLedger
{
    /// <summary>
    /// Append rated samples. Idempotent: rows whose (EventId, Direction) already exist are
    /// skipped. Returns the number of rows actually written.
    /// </summary>
    Task<int> AddAsync(IReadOnlyList<UsageRecord> records, CancellationToken ct);

    /// <summary>Aggregate AI-token usage + cost over an optional window, with breakdowns.</summary>
    Task<UsageSummaryDto> GetSummaryAsync(DateTimeOffset? fromUtc, DateTimeOffset? toUtc, CancellationToken ct);

    /// <summary>Totals per <c>MeterKind</c> across all meters (AI + build/deploy activity).</summary>
    Task<IReadOnlyList<MeterTotalDto>> GetMeterTotalsAsync(DateTimeOffset? fromUtc, DateTimeOffset? toUtc, CancellationToken ct);
}
