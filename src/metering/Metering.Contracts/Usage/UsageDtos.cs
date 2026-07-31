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

/// <summary>
/// One point-in-time sample of a level: bytes in a repository, pods in a namespace.
///
/// Deliberately separate from <see cref="IngestAiUsageRequest"/> rather than a shared shape with
/// nullable fields. That request is token-shaped — four directions and a model — and none of it
/// means anything for storage. Two small honest DTOs beat one with half its fields always null.
/// </summary>
public sealed record IngestGaugeRequest(
    Guid EventId,
    /// <summary>A <c>MeterKind</c> name. AiTokens is rejected — it has its own endpoint.</summary>
    string Meter,
    double Quantity,
    string Unit,
    DateTimeOffset OccurredAtUtc,
    string Source,
    string? Repository = null,
    string? Service = null,
    string? Environment = null);

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
    decimal CostUsd,
    // Gauges and counters are not the same measurement and must not be read the same way.
    // For a counter, Quantity is the SUM over the window — tokens, jobs, runs all accumulate.
    // For a gauge it is the LATEST sample per series, summed across series: storage bytes are a
    // level, not a flow, and adding successive samples of the same repository would report a
    // number that grows purely because the collector ran more often.
    bool IsGauge = false,
    // When the gauge was last sampled. Null for counters, where the window itself is the answer.
    DateTimeOffset? AsOfUtc = null);
