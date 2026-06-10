using Jenkins.Contracts.Pipelines;
using Jenkins.Domain.Abstractions;
using Jenkins.Domain.Pipelines;

namespace Jenkins.Application.Features.Pipelines;

/// <summary>Read-model port for the pipeline list (flat summaries).</summary>
public interface IPipelineReader
{
    Task<IReadOnlyList<PipelineSummaryDto>> ListAsync(CancellationToken cancellationToken = default);
}

public sealed record ListPipelinesQuery;

public sealed class ListPipelinesHandler
{
    private readonly IPipelineReader _reader;
    public ListPipelinesHandler(IPipelineReader reader) => _reader = reader;

    public Task<IReadOnlyList<PipelineSummaryDto>> HandleAsync(ListPipelinesQuery query, CancellationToken cancellationToken = default)
        => _reader.ListAsync(cancellationToken);
}

public sealed record GetPipelineByIdQuery(Guid Id);

public sealed class GetPipelineByIdHandler
{
    private readonly IPipelineStore _pipelines;
    public GetPipelineByIdHandler(IPipelineStore pipelines) => _pipelines = pipelines;

    public async Task<PipelineDto?> HandleAsync(GetPipelineByIdQuery query, CancellationToken cancellationToken = default)
    {
        var pipeline = await _pipelines.GetByIdAsync(query.Id, cancellationToken).ConfigureAwait(false);
        return pipeline?.ToDto();
    }
}

/// <summary>
/// Seeds the default "CICD Main" pipeline (build → publish NuGet + container to
/// Nexus) when no pipelines exist yet — preserves the previously-hardcoded chain.
/// Called once at host startup; idempotent.
/// </summary>
public sealed class SeedDefaultPipelineHandler
{
    private readonly IPipelineStore _pipelines;
    private readonly IUnitOfWork _uow;
    private readonly TimeProvider _clock;

    public SeedDefaultPipelineHandler(IPipelineStore pipelines, IUnitOfWork uow, TimeProvider clock)
    {
        _pipelines = pipelines;
        _uow = uow;
        _clock = clock;
    }

    public async Task HandleAsync(CancellationToken cancellationToken = default)
    {
        if (await _pipelines.AnyAsync(cancellationToken).ConfigureAwait(false)) return;

        var now = _clock.GetUtcNow();
        var pipeline = new Pipeline(Guid.NewGuid(), "CICD Main",
            "Build, then publish the NuGet package and container image to Nexus.", now);
        pipeline.AddStage(Guid.NewGuid(), "cicd-build", null, null, now);
        pipeline.AddStage(Guid.NewGuid(), "cicd-publish-nexus-nuget", "cicd-build", null, now);
        pipeline.AddStage(Guid.NewGuid(), "cicd-publish-nexus-docker", "cicd-build", null, now);

        await _pipelines.AddAsync(pipeline, cancellationToken).ConfigureAwait(false);
        await _uow.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
