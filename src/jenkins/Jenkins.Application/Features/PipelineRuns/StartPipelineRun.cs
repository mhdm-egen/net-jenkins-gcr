using Jenkins.Application.Abstractions;
using Jenkins.Domain.Abstractions;
using Jenkins.Domain.Pipelines;
using Jenkins.Domain.PipelineRuns;
using FluentValidation;

namespace Jenkins.Application.Features.PipelineRuns;

/// <summary>
/// Start a server-side run of a persisted pipeline. Loads the pipeline definition, creates a
/// Running <see cref="PipelineRun"/>, persists it (raising PipelineRunStarted), and enqueues it
/// for the background executor. Returns the new run id.
/// </summary>
public sealed record StartPipelineRunCommand(Guid PipelineId, Guid? RepositoryId, string? TriggeredBy);

public sealed class StartPipelineRunValidator : AbstractValidator<StartPipelineRunCommand>
{
    public StartPipelineRunValidator()
    {
        RuleFor(x => x.PipelineId).NotEmpty();
    }
}

public sealed class StartPipelineRunHandler
{
    private readonly IPipelineStore _pipelines;
    private readonly IPipelineRunStore _runs;
    private readonly IPipelineRunQueue _queue;
    private readonly IUnitOfWork _uow;
    private readonly TimeProvider _clock;

    public StartPipelineRunHandler(
        IPipelineStore pipelines,
        IPipelineRunStore runs,
        IPipelineRunQueue queue,
        IUnitOfWork uow,
        TimeProvider clock)
    {
        _pipelines = pipelines;
        _runs = runs;
        _queue = queue;
        _uow = uow;
        _clock = clock;
    }

    public async Task<Guid> HandleAsync(StartPipelineRunCommand cmd, CancellationToken cancellationToken = default)
    {
        var pipeline = await _pipelines.GetByIdAsync(cmd.PipelineId, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Pipeline {cmd.PipelineId} not found.");
        if (pipeline.Stages.Count == 0)
            throw new InvalidOperationException("Pipeline has no stages to run.");

        var run = new PipelineRun(
            id: Guid.NewGuid(),
            pipelineId: pipeline.Id,
            pipelineName: pipeline.Name,
            repositoryId: cmd.RepositoryId,
            triggeredBy: cmd.TriggeredBy ?? "unknown",
            startedAtUtc: _clock.GetUtcNow());

        await _runs.AddAsync(run, cancellationToken).ConfigureAwait(false);
        await _uow.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await _queue.EnqueueAsync(run.Id, cancellationToken).ConfigureAwait(false);
        return run.Id;
    }
}
