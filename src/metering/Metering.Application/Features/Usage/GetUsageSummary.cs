using Metering.Application.Abstractions;
using Metering.Contracts.Usage;

namespace Metering.Application.Features.Usage;

public sealed record GetUsageSummaryQuery(DateTimeOffset? FromUtc, DateTimeOffset? ToUtc);

/// <summary>Returns the rated AI-token usage rollup over an optional window.</summary>
public sealed class GetUsageSummaryHandler
{
    private readonly IUsageLedger _ledger;

    public GetUsageSummaryHandler(IUsageLedger ledger) => _ledger = ledger;

    public Task<UsageSummaryDto> HandleAsync(GetUsageSummaryQuery query, CancellationToken ct)
        => _ledger.GetSummaryAsync(query.FromUtc, query.ToUtc, ct);
}
