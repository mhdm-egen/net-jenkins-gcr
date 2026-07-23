namespace Metering.Contracts.Usage;

/// <summary>
/// AI token-usage ingest payload (web-admin → metering-api, HTTP). The four token
/// counts come straight off the Anthropic <c>usage</c> block; <see cref="EventId"/> is
/// the idempotency key. The service rates + expands this into per-direction ledger rows.
/// </summary>
public sealed record IngestAiUsageRequest(
    Guid EventId,
    string Feature,
    string Model,
    string Source,
    long InputTokens,
    long OutputTokens,
    long CacheReadTokens,
    long CacheWriteTokens,
    DateTimeOffset OccurredAtUtc,
    string? Repository = null,
    string? Service = null,
    string? Environment = null);

/// <summary>Acknowledgement of an ingest — rows written + computed cost.</summary>
public sealed record IngestAckDto(Guid EventId, int RowsWritten, decimal CostUsd);

/// <summary>Rolled-up usage + cost over a window, with by-model / by-feature breakdowns.</summary>
public sealed record UsageSummaryDto(
    int CallCount,
    long InputTokens,
    long OutputTokens,
    long CacheReadTokens,
    long CacheWriteTokens,
    decimal CostUsd,
    double CacheHitRate,
    IReadOnlyList<UsageByModelDto> ByModel,
    IReadOnlyList<UsageByFeatureDto> ByFeature);

public sealed record UsageByModelDto(
    string Model,
    long InputTokens,
    long OutputTokens,
    long CacheReadTokens,
    long CacheWriteTokens,
    decimal CostUsd);

public sealed record UsageByFeatureDto(
    string Feature,
    int CallCount,
    long InputTokens,
    long OutputTokens,
    decimal CostUsd);

/// <summary>General by-meter rollup across all meter kinds (AI tokens, build/deploy activity, …).</summary>
public sealed record MeterTotalDto(
    string Meter,
    int Records,
    double Quantity,
    string Unit,
    decimal CostUsd);
