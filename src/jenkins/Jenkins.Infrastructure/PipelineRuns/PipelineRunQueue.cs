using System.Threading.Channels;
using Jenkins.Application.Abstractions;

namespace Jenkins.Infrastructure.PipelineRuns;

/// <summary>Unbounded in-memory queue of run ids feeding the single executor loop.</summary>
public sealed class PipelineRunQueue : IPipelineRunQueue
{
    private readonly Channel<Guid> _channel =
        Channel.CreateUnbounded<Guid>(new UnboundedChannelOptions { SingleReader = true });

    public ValueTask EnqueueAsync(Guid runId, CancellationToken cancellationToken = default)
        => _channel.Writer.WriteAsync(runId, cancellationToken);

    public IAsyncEnumerable<Guid> DequeueAllAsync(CancellationToken cancellationToken)
        => _channel.Reader.ReadAllAsync(cancellationToken);
}
