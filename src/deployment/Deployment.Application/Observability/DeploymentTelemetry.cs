using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Deployment.Application.Observability;

/// <summary>
/// Custom OpenTelemetry instrumentation for the deployment service: a <see cref="Meter"/> of deploy
/// metrics and an <see cref="ActivitySource"/> for deploy-run spans. Registered as a singleton and wired
/// into the OTel pipeline via <c>AddMeter</c>/<c>AddSource</c> in Program.cs, so it flows to the Aspire
/// dashboard (and any OTLP backend). Instruments are named under <c>cicd.deploy.*</c>.
/// </summary>
public sealed class DeploymentTelemetry : IDisposable
{
    public const string Name = "Cicd.Deployment";

    /// <summary>Spans for a deploy run (start in the executor, tagged with the outcome on settle).</summary>
    public static readonly ActivitySource Activity = new(Name);

    private readonly Meter _meter;
    private readonly Counter<long> _runs;
    private readonly Histogram<double> _duration;

    public DeploymentTelemetry()
    {
        _meter = new Meter(Name);
        _runs = _meter.CreateCounter<long>("cicd.deploy.runs", unit: "{run}", description: "Deployment runs that reached a terminal state, tagged by target/outcome/strategy.");
        _duration = _meter.CreateHistogram<double>("cicd.deploy.duration", unit: "s", description: "Deployment run duration in seconds.");
    }

    /// <summary>Record a terminal deploy run. <paramref name="target"/> is cloudrun/kubernetes/aspire;
    /// <paramref name="outcome"/> is Succeeded/Failed/RolledBack; <paramref name="strategy"/> is the rollout
    /// strategy for a Kubernetes deploy (else null).</summary>
    public void RecordRun(string target, string outcome, string? strategy, double durationSeconds)
    {
        var tags = new TagList { { "target", target }, { "outcome", outcome } };
        if (!string.IsNullOrWhiteSpace(strategy)) tags.Add("strategy", strategy);
        _runs.Add(1, tags);
        if (durationSeconds >= 0) _duration.Record(durationSeconds, tags);
    }

    public void Dispose() => _meter.Dispose();
}
