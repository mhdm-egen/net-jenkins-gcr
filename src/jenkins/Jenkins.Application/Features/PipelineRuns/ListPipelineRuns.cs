using Jenkins.Contracts.PipelineRuns;

namespace Jenkins.Application.Features.PipelineRuns;

/// <summary>
/// Read-model port for pipeline-run history/detail. The handlers delegate to the reader so
/// projections can stay flat. Implemented by <c>EfPipelineRunReader</c> in Infrastructure.
/// </summary>
public interface IPipelineRunReader
{
    Task<IReadOnlyList<PipelineRunSummaryDto>> ListAsync(Guid? pipelineId, int take, CancellationToken cancellationToken = default);
    Task<PipelineRunDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PipelineRunConsoleDto>> GetConsoleAsync(Guid runId, CancellationToken cancellationToken = default);
}

public sealed record ListPipelineRunsQuery(Guid? PipelineId, int Take);

public sealed class ListPipelineRunsHandler
{
    private readonly IPipelineRunReader _reader;
    public ListPipelineRunsHandler(IPipelineRunReader reader) => _reader = reader;

    public Task<IReadOnlyList<PipelineRunSummaryDto>> HandleAsync(ListPipelineRunsQuery query, CancellationToken cancellationToken = default)
        => _reader.ListAsync(query.PipelineId, query.Take <= 0 ? 50 : query.Take, cancellationToken);
}

public sealed record GetPipelineRunByIdQuery(Guid Id);

public sealed class GetPipelineRunByIdHandler
{
    private readonly IPipelineRunReader _reader;
    public GetPipelineRunByIdHandler(IPipelineRunReader reader) => _reader = reader;

    public Task<PipelineRunDto?> HandleAsync(GetPipelineRunByIdQuery query, CancellationToken cancellationToken = default)
        => _reader.GetByIdAsync(query.Id, cancellationToken);
}

public sealed record GetPipelineRunConsoleQuery(Guid Id);

public sealed class GetPipelineRunConsoleHandler
{
    private readonly IPipelineRunReader _reader;
    public GetPipelineRunConsoleHandler(IPipelineRunReader reader) => _reader = reader;

    public Task<IReadOnlyList<PipelineRunConsoleDto>> HandleAsync(GetPipelineRunConsoleQuery query, CancellationToken cancellationToken = default)
        => _reader.GetConsoleAsync(query.Id, cancellationToken);
}
