namespace Cicd.Ai;

/// <summary>
/// Sink for token usage captured at the <c>AiClient</c> boundary. Two implementations are
/// registered and fanned out through <see cref="CompositeAiUsageRecorder"/>:
/// <see cref="MeterAiUsageRecorder"/> (OpenTelemetry meter <c>Cicd.Ai</c> + structured log) and
/// <see cref="MeteringUsageRecorder"/> (HTTP ingest into the metering ledger, which is
/// the authoritative record).
///
/// This was originally planned as an <c>AiTokensConsumed</c> integration event over the Wolverine
/// outbox; it shipped as fire-and-forget HTTP instead because web-admin is a Blazor UI host rather
/// than a bus participant. Keeping this a seam is what made that substitution a one-file change.
/// </summary>
public interface IAiUsageRecorder
{
    void Record(
        string feature,
        string model,
        AiUsage usage,
        IReadOnlyDictionary<string, string>? dimensions);
}
