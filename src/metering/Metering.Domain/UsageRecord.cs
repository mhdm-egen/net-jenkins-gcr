namespace Metering.Domain;

/// <summary>
/// One immutable metered sample in the ledger. The general shape (quantity + unit +
/// dimensions + rated cost) holds for every <see cref="MeterKind"/>; for AI tokens one
/// AI call expands into one record per <see cref="Direction"/> (input / output /
/// cache_read / cache_write), each independently rated. <see cref="EventId"/> makes
/// ingest idempotent (dedupe on retry).
/// </summary>
public sealed class UsageRecord
{
    public Guid Id { get; init; }

    /// <summary>Producer-supplied idempotency key — a duplicate EventId+Direction is ignored.</summary>
    public Guid EventId { get; init; }

    public MeterKind Meter { get; init; }
    public MeterType MeterType { get; init; }

    /// <summary>The metered amount in <see cref="Unit"/> (e.g. tokens, bytes, seconds).</summary>
    public double Quantity { get; init; }

    public string Unit { get; init; } = "count";

    /// <summary>For AI: input / output / cache_read / cache_write. Empty otherwise.</summary>
    public string Direction { get; init; } = string.Empty;

    /// <summary>Attribution key — the feature that produced the usage (e.g. "explain_cve").</summary>
    public string Feature { get; init; } = string.Empty;

    /// <summary>Model id for AI meters (e.g. "claude-sonnet-5"); empty otherwise.</summary>
    public string Model { get; init; } = string.Empty;

    /// <summary>Producing service (e.g. "web-admin").</summary>
    public string Source { get; init; } = string.Empty;

    // Common showback dimensions (nullable — populated when known).
    public string? Repository { get; init; }
    public string? Service { get; init; }
    public string? Environment { get; init; }

    /// <summary>Cost snapshot computed at ingest against the versioned rate table.</summary>
    public decimal CostUsd { get; init; }

    /// <summary>Rate-table version used to compute <see cref="CostUsd"/> (for repricing/audit).</summary>
    public string RateVersion { get; init; } = string.Empty;

    public DateTimeOffset OccurredAtUtc { get; init; }
}
