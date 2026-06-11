using System.Collections.Concurrent;
using Jenkins.Application.Abstractions;

namespace Jenkins.Infrastructure.PipelineRuns;

/// <summary>
/// Registry of in-flight run cancellation sources. The executor tracks each run's linked CTS
/// while it runs; a cancel request cancels it. Single-instance only.
/// </summary>
internal sealed class PipelineRunCancellation : IPipelineRunCancellation
{
    private readonly ConcurrentDictionary<Guid, CancellationTokenSource> _inFlight = new();

    public void Track(Guid runId, CancellationTokenSource cts) => _inFlight[runId] = cts;

    public void Forget(Guid runId) => _inFlight.TryRemove(runId, out _);

    public bool Cancel(Guid runId)
    {
        if (!_inFlight.TryGetValue(runId, out var cts)) return false;
        try { cts.Cancel(); } catch (ObjectDisposedException) { /* already settled */ }
        return true;
    }
}
