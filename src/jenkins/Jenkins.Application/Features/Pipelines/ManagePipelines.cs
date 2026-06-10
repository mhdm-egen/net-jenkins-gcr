using Jenkins.Contracts.Pipelines;
using Jenkins.Domain.Abstractions;
using Jenkins.Domain.Pipelines;
using FluentValidation;

namespace Jenkins.Application.Features.Pipelines;

// --- Create ---

public sealed record CreatePipelineCommand(Guid Id, string Name, string? Description);

public sealed class CreatePipelineValidator : AbstractValidator<CreatePipelineCommand>
{
    public CreatePipelineValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Description).MaximumLength(1000);
    }
}

public sealed class CreatePipelineHandler
{
    private readonly IPipelineStore _pipelines;
    private readonly IUnitOfWork _uow;
    private readonly TimeProvider _clock;

    public CreatePipelineHandler(IPipelineStore pipelines, IUnitOfWork uow, TimeProvider clock)
    {
        _pipelines = pipelines;
        _uow = uow;
        _clock = clock;
    }

    public async Task<PipelineDto> HandleAsync(CreatePipelineCommand cmd, CancellationToken cancellationToken = default)
    {
        if (await _pipelines.FindByNameAsync(cmd.Name, cancellationToken).ConfigureAwait(false) is not null)
            throw new InvalidOperationException($"A pipeline named '{cmd.Name}' already exists.");

        var pipeline = new Pipeline(cmd.Id, cmd.Name, cmd.Description, _clock.GetUtcNow());
        await _pipelines.AddAsync(pipeline, cancellationToken).ConfigureAwait(false);
        await _uow.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return pipeline.ToDto();
    }
}

// --- Update (rename + description) ---

public sealed record UpdatePipelineCommand(Guid Id, string Name, string? Description);

public sealed class UpdatePipelineValidator : AbstractValidator<UpdatePipelineCommand>
{
    public UpdatePipelineValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Description).MaximumLength(1000);
    }
}

public sealed class UpdatePipelineHandler
{
    private readonly IPipelineStore _pipelines;
    private readonly IUnitOfWork _uow;
    private readonly TimeProvider _clock;

    public UpdatePipelineHandler(IPipelineStore pipelines, IUnitOfWork uow, TimeProvider clock)
    {
        _pipelines = pipelines;
        _uow = uow;
        _clock = clock;
    }

    public async Task<PipelineDto> HandleAsync(UpdatePipelineCommand cmd, CancellationToken cancellationToken = default)
    {
        var pipeline = await _pipelines.GetByIdAsync(cmd.Id, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Pipeline {cmd.Id} not found.");

        // Guard the unique-name invariant when the name actually changes.
        if (!string.Equals(pipeline.Name, cmd.Name.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            var clash = await _pipelines.FindByNameAsync(cmd.Name, cancellationToken).ConfigureAwait(false);
            if (clash is not null && clash.Id != pipeline.Id)
                throw new InvalidOperationException($"A pipeline named '{cmd.Name}' already exists.");
        }

        pipeline.Rename(cmd.Name, cmd.Description, _clock.GetUtcNow());
        await _uow.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return pipeline.ToDto();
    }
}

// --- Activate / deactivate ---

public sealed record SetPipelineActiveCommand(Guid Id, bool IsActive);

public sealed class SetPipelineActiveHandler
{
    private readonly IPipelineStore _pipelines;
    private readonly IUnitOfWork _uow;
    private readonly TimeProvider _clock;

    public SetPipelineActiveHandler(IPipelineStore pipelines, IUnitOfWork uow, TimeProvider clock)
    {
        _pipelines = pipelines;
        _uow = uow;
        _clock = clock;
    }

    public async Task<PipelineDto> HandleAsync(SetPipelineActiveCommand cmd, CancellationToken cancellationToken = default)
    {
        var pipeline = await _pipelines.GetByIdAsync(cmd.Id, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Pipeline {cmd.Id} not found.");
        pipeline.SetActive(cmd.IsActive, _clock.GetUtcNow());
        await _uow.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return pipeline.ToDto();
    }
}

// --- Delete ---

public sealed record DeletePipelineCommand(Guid Id);

public sealed class DeletePipelineHandler
{
    private readonly IPipelineStore _pipelines;
    private readonly IUnitOfWork _uow;

    public DeletePipelineHandler(IPipelineStore pipelines, IUnitOfWork uow)
    {
        _pipelines = pipelines;
        _uow = uow;
    }

    public async Task HandleAsync(DeletePipelineCommand cmd, CancellationToken cancellationToken = default)
    {
        var pipeline = await _pipelines.GetByIdAsync(cmd.Id, cancellationToken).ConfigureAwait(false);
        if (pipeline is null) return; // idempotent

        _pipelines.Remove(pipeline);
        await _uow.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
