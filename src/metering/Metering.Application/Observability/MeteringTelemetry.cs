using System.Diagnostics.Metrics;

namespace Metering.Application.Observability;

/// <summary>
/// The metering service's own OpenTelemetry meter (<c>Cicd.Metering</c>, mirroring the
/// deployment service's <c>Cicd.Deployment</c>). Records what the ledger ingests so
/// spend is observable live, independent of the persisted rollups. Registered as a
/// singleton and wired into the OTel pipeline in Program.cs.
/// </summary>
public sealed class MeteringTelemetry : IDisposable
{
    public const string Name = "Cicd.Metering";

    private readonly Meter _meter;
    private readonly Counter<long> _tokens;
    private readonly Counter<double> _cost;

    public MeteringTelemetry()
    {
        _meter = new Meter(Name);
        _tokens = _meter.CreateCounter<long>("cicd.metering.tokens", unit: "{token}",
            description: "AI tokens ingested into the ledger, tagged by direction/model.");
        _cost = _meter.CreateCounter<double>("cicd.metering.cost.usd", unit: "USD",
            description: "Rated cost ingested into the ledger, tagged by model.");
    }

    public void RecordAiTokens(string model, string direction, long tokens, decimal costUsd)
    {
        if (tokens > 0)
            _tokens.Add(tokens,
                new KeyValuePair<string, object?>("direction", direction),
                new KeyValuePair<string, object?>("model", model));
        if (costUsd > 0)
            _cost.Add((double)costUsd, new KeyValuePair<string, object?>("model", model));
    }

    public void Dispose() => _meter.Dispose();
}
