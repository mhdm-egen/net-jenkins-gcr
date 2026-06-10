using Jenkins.Contracts.Pipelines;
using Jenkins.Domain.Abstractions;
using Jenkins.Domain.Pipelines;
using FluentValidation;

namespace Jenkins.Application.Features.Pipelines;

// All stage mutations return the updated PipelineDto so the UI can refresh in one call.

// --- Add ---

public sealed record AddStageCommand(
    Guid PipelineId,
    Guid StageId,
    string JobName,
    string? UpstreamJobName,
    IReadOnlyDictionary<string, string>? Parameters);

public sealed class AddStageValidator : AbstractValidator<AddStageCommand>
{
    public AddStageValidator()
    {
        RuleFor(x => x.PipelineId).NotEmpty();
        RuleFor(x => x.StageId).NotEmpty();
        RuleFor(x => x.JobName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.UpstreamJobName).MaximumLength(200);
    }
}

public sealed class AddStageHandler
{
    private readonly IPipelineStore _pipelines;
    private readonly IUnitOfWork _uow;
    private readonly TimeProvider _clock;

    public AddStageHandler(IPipelineStore pipelines, IUnitOfWork uow, TimeProvider clock)
    {
        _pipelines = pipelines;
        _uow = uow;
        _clock = clock;
    }

    public async Task<PipelineDto> HandleAsync(AddStageCommand cmd, CancellationToken cancellationToken = default)
    {
        var pipeline = await Load(_pipelines, cmd.PipelineId, cancellationToken).ConfigureAwait(false);
        pipeline.AddStage(cmd.StageId, cmd.JobName, cmd.UpstreamJobName, cmd.Parameters, _clock.GetUtcNow());
        await _uow.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return pipeline.ToDto();
    }

    internal static async Task<Pipeline> Load(IPipelineStore store, Guid id, CancellationToken ct) =>
        await store.GetByIdAsync(id, ct).ConfigureAwait(false)
        ?? throw new InvalidOperationException($"Pipeline {id} not found.");
}

// --- Update ---

public sealed record UpdateStageCommand(
    Guid PipelineId,
    Guid StageId,
    string JobName,
    string? UpstreamJobName,
    IReadOnlyDictionary<string, string>? Parameters);

public sealed class UpdateStageValidator : AbstractValidator<UpdateStageCommand>
{
    public UpdateStageValidator()
    {
        RuleFor(x => x.PipelineId).NotEmpty();
        RuleFor(x => x.StageId).NotEmpty();
        RuleFor(x => x.JobName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.UpstreamJobName).MaximumLength(200);
    }
}

public sealed class UpdateStageHandler
{
    private readonly IPipelineStore _pipelines;
    private readonly IUnitOfWork _uow;
    private readonly TimeProvider _clock;

    public UpdateStageHandler(IPipelineStore pipelines, IUnitOfWork uow, TimeProvider clock)
    {
        _pipelines = pipelines;
        _uow = uow;
        _clock = clock;
    }

    public async Task<PipelineDto> HandleAsync(UpdateStageCommand cmd, CancellationToken cancellationToken = default)
    {
        var pipeline = await AddStageHandler.Load(_pipelines, cmd.PipelineId, cancellationToken).ConfigureAwait(false);
        pipeline.UpdateStage(cmd.StageId, cmd.JobName, cmd.UpstreamJobName, cmd.Parameters, _clock.GetUtcNow());
        await _uow.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return pipeline.ToDto();
    }
}

// --- Remove ---

public sealed record RemoveStageCommand(Guid PipelineId, Guid StageId);

public sealed class RemoveStageHandler
{
    private readonly IPipelineStore _pipelines;
    private readonly IUnitOfWork _uow;
    private readonly TimeProvider _clock;

    public RemoveStageHandler(IPipelineStore pipelines, IUnitOfWork uow, TimeProvider clock)
    {
        _pipelines = pipelines;
        _uow = uow;
        _clock = clock;
    }

    public async Task<PipelineDto> HandleAsync(RemoveStageCommand cmd, CancellationToken cancellationToken = default)
    {
        var pipeline = await AddStageHandler.Load(_pipelines, cmd.PipelineId, cancellationToken).ConfigureAwait(false);
        pipeline.RemoveStage(cmd.StageId, _clock.GetUtcNow());
        await _uow.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return pipeline.ToDto();
    }
}

// --- Reorder ---

public sealed record ReorderStagesCommand(Guid PipelineId, IReadOnlyList<Guid> OrderedStageIds);

public sealed class ReorderStagesValidator : AbstractValidator<ReorderStagesCommand>
{
    public ReorderStagesValidator()
    {
        RuleFor(x => x.PipelineId).NotEmpty();
        RuleFor(x => x.OrderedStageIds).NotEmpty();
    }
}

public sealed class ReorderStagesHandler
{
    private readonly IPipelineStore _pipelines;
    private readonly IUnitOfWork _uow;
    private readonly TimeProvider _clock;

    public ReorderStagesHandler(IPipelineStore pipelines, IUnitOfWork uow, TimeProvider clock)
    {
        _pipelines = pipelines;
        _uow = uow;
        _clock = clock;
    }

    public async Task<PipelineDto> HandleAsync(ReorderStagesCommand cmd, CancellationToken cancellationToken = default)
    {
        var pipeline = await AddStageHandler.Load(_pipelines, cmd.PipelineId, cancellationToken).ConfigureAwait(false);
        pipeline.ReorderStages(cmd.OrderedStageIds, _clock.GetUtcNow());
        await _uow.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return pipeline.ToDto();
    }
}
