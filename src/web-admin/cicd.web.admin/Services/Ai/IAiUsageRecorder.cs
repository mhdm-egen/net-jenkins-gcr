namespace Cicd.Web.Admin.Services.Ai;

/// <summary>
/// Sink for token usage captured at the <c>AiClient</c> boundary. The Phase-0
/// implementation emits an OpenTelemetry meter (<c>Cicd.Ai</c>) + structured log. When
/// the metering microservice lands, an implementation additionally publishes an
/// <c>AiTokensConsumed</c> integration event through the Wolverine outbox and the
/// ledger becomes the authoritative record. Keeping this a seam means web-admin does
/// not take a messaging dependency until that point.
/// </summary>
public interface IAiUsageRecorder
{
    void Record(
        string feature,
        string model,
        AiUsage usage,
        IReadOnlyDictionary<string, string>? dimensions);
}
