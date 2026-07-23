namespace Cicd.Web.Admin.Services.Ai;

/// <summary>
/// Fans usage out to several recorders (the local OTel meter + the metering-api HTTP
/// ingest). Each is isolated: a failing recorder never blocks the others or the caller.
/// </summary>
public sealed class CompositeAiUsageRecorder : IAiUsageRecorder
{
    private readonly IReadOnlyList<IAiUsageRecorder> _recorders;

    public CompositeAiUsageRecorder(params IAiUsageRecorder[] recorders) => _recorders = recorders;

    public void Record(
        string feature,
        string model,
        AiUsage usage,
        IReadOnlyDictionary<string, string>? dimensions)
    {
        foreach (var recorder in _recorders)
        {
            try { recorder.Record(feature, model, usage, dimensions); }
            catch { /* isolate — usage recording must never affect the AI call */ }
        }
    }
}
