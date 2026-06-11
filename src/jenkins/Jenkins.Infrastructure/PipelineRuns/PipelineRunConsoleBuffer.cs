using System.Collections.Concurrent;
using System.Text;
using Jenkins.Application.Abstractions;

namespace Jenkins.Infrastructure.PipelineRuns;

/// <summary>
/// Bounded per-(run, job) console buffer for replay on (re)connect. Single-instance only —
/// a scaled-out host would need a shared store + SignalR backplane.
/// </summary>
internal sealed class PipelineRunConsoleBuffer : IPipelineRunConsoleBuffer
{
    private const int PerJobCharCap = 1_000_000;
    private readonly ConcurrentDictionary<Guid, ConcurrentDictionary<string, StringBuilder>> _runs = new();

    public void Append(Guid runId, string jobName, string chunk)
    {
        if (string.IsNullOrEmpty(chunk)) return;
        var jobs = _runs.GetOrAdd(runId, _ => new ConcurrentDictionary<string, StringBuilder>());
        var sb = jobs.GetOrAdd(jobName, _ => new StringBuilder());
        lock (sb)
        {
            if (sb.Length < PerJobCharCap) sb.Append(chunk);
        }
    }

    public IReadOnlyList<PipelineRunConsoleSegment> Snapshot(Guid runId)
    {
        if (!_runs.TryGetValue(runId, out var jobs)) return Array.Empty<PipelineRunConsoleSegment>();
        var list = new List<PipelineRunConsoleSegment>();
        foreach (var kv in jobs)
        {
            string text;
            lock (kv.Value) { text = kv.Value.ToString(); }
            list.Add(new PipelineRunConsoleSegment(kv.Key, text));
        }
        return list;
    }

    public void Clear(Guid runId) => _runs.TryRemove(runId, out _);
}
