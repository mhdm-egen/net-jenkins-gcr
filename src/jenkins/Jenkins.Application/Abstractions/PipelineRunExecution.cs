namespace Jenkins.Application.Abstractions;

/// <summary>
/// Hands an enqueued pipeline run to the background executor. Implemented as a singleton
/// in-memory channel in Infrastructure.
/// </summary>
public interface IPipelineRunQueue
{
    ValueTask EnqueueAsync(Guid runId, CancellationToken cancellationToken = default);
    IAsyncEnumerable<Guid> DequeueAllAsync(CancellationToken cancellationToken);
}

/// <summary>
/// Pushes live run updates to subscribed clients (SignalR). Implemented in the API host where
/// the hub lives; the Infrastructure executor depends only on this port.
/// </summary>
public interface IPipelineRunNotifier
{
    Task StepChangedAsync(Guid runId, PipelineRunStepUpdate step, CancellationToken cancellationToken = default);
    Task ConsoleAppendedAsync(Guid runId, string jobName, int buildNumber, string text, CancellationToken cancellationToken = default);
    Task RunSettledAsync(Guid runId, string status, string? failureReason, CancellationToken cancellationToken = default);
}

public sealed record PipelineRunStepUpdate(string JobName, string Status, int? BuildNumber, string? Reason);

/// <summary>
/// Bounded in-memory console buffer per active run so a (re)connecting client can replay the
/// current output. Written by the executor, read by the hub on subscribe. Singleton.
/// </summary>
public interface IPipelineRunConsoleBuffer
{
    void Append(Guid runId, string jobName, string chunk);
    IReadOnlyList<PipelineRunConsoleSegment> Snapshot(Guid runId);
    void Clear(Guid runId);
}

public sealed record PipelineRunConsoleSegment(string JobName, string Text);
