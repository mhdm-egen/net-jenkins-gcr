using Jenkins.Application.Abstractions;

namespace Jenkins.Application.Features.PipelineRuns;

/// <summary>
/// Request cancellation of an in-flight run. Trips the run's cancellation token; the executor
/// stops the in-flight Jenkins build and settles the run Cancelled. Returns false when the run
/// isn't currently executing (already finished, or not yet picked up).
/// </summary>
public sealed record CancelPipelineRunCommand(Guid RunId);

public sealed class CancelPipelineRunHandler
{
    private readonly IPipelineRunCancellation _cancellation;

    public CancelPipelineRunHandler(IPipelineRunCancellation cancellation) => _cancellation = cancellation;

    public Task<bool> HandleAsync(CancelPipelineRunCommand cmd, CancellationToken cancellationToken = default)
        => Task.FromResult(_cancellation.Cancel(cmd.RunId));
}
