using Metering.Application.Abstractions;
using Metering.Contracts.Usage;

namespace Metering.Application.Features.Usage;

public sealed record GetMeterTotalsQuery(DateTimeOffset? FromUtc, DateTimeOffset? ToUtc);

/// <summary>Returns the by-meter rollup across every meter kind in the ledger.</summary>
public sealed class GetMeterTotalsHandler
{
    private readonly IUsageLedger _ledger;

    public GetMeterTotalsHandler(IUsageLedger ledger) => _ledger = ledger;

    public Task<IReadOnlyList<MeterTotalDto>> HandleAsync(GetMeterTotalsQuery query, CancellationToken ct)
        => _ledger.GetMeterTotalsAsync(query.FromUtc, query.ToUtc, ct);
}
