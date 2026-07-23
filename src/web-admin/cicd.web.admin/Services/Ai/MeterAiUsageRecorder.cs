using System.Diagnostics.Metrics;

namespace Cicd.Web.Admin.Services.Ai;

/// <summary>
/// Phase-0 <see cref="IAiUsageRecorder"/>: emits an OpenTelemetry meter (<c>Cicd.Ai</c>,
/// mirroring the deployment service's <c>Cicd.Deployment</c> meter) plus a structured
/// log. When the metering microservice lands, a bus-publishing recorder augments/replaces
/// this and the ledger becomes authoritative. The <c>Cicd.Ai</c> meter must be added to
/// the OTel MeterProvider (see ServiceDefaults / Program.cs registration).
/// </summary>
public sealed class MeterAiUsageRecorder : IAiUsageRecorder
{
    public const string MeterName = "Cicd.Ai";

    private readonly ILogger<MeterAiUsageRecorder> _log;
    private readonly Counter<long> _tokens;

    public MeterAiUsageRecorder(IMeterFactory meterFactory, ILogger<MeterAiUsageRecorder> log)
    {
        _log = log;
        var meter = meterFactory.Create(MeterName);
        _tokens = meter.CreateCounter<long>("cicd.ai.tokens", unit: "{token}",
            description: "Claude tokens consumed by AI features, tagged by direction/feature/model.");
    }

    public void Record(
        string feature,
        string model,
        AiUsage usage,
        IReadOnlyDictionary<string, string>? dimensions)
    {
        Add(usage.InputTokens, "input", feature, model);
        Add(usage.OutputTokens, "output", feature, model);
        Add(usage.CacheReadInputTokens, "cache_read", feature, model);
        Add(usage.CacheCreationInputTokens, "cache_write", feature, model);

        _log.LogInformation(
            "AI usage feature={Feature} model={Model} in={In} out={Out} cacheRead={CacheRead} cacheWrite={CacheWrite}",
            feature, model, usage.InputTokens, usage.OutputTokens,
            usage.CacheReadInputTokens, usage.CacheCreationInputTokens);
    }

    private void Add(long value, string direction, string feature, string model)
    {
        if (value <= 0) return;
        _tokens.Add(value,
            new KeyValuePair<string, object?>("direction", direction),
            new KeyValuePair<string, object?>("feature", feature),
            new KeyValuePair<string, object?>("model", model));
    }
}
