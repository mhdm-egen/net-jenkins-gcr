using Deployment.Application.Features.Environments;
using Deployment.Contracts.Environments;
using Deployment.Domain.Abstractions;
using Deployment.Domain.Environments;
using FluentValidation;

namespace Deployment.Application.Features.Environments.ManageTargets;

// --- Add ---

public sealed record AddTargetCommand(
    Guid EnvironmentId,
    Guid TargetId,
    TargetKindDto TargetKind,
    string ResourceId,
    string Region,
    string? Slot);

public sealed class AddTargetValidator : AbstractValidator<AddTargetCommand>
{
    public AddTargetValidator()
    {
        RuleFor(x => x.EnvironmentId).NotEmpty();
        RuleFor(x => x.TargetId).NotEmpty();
        RuleFor(x => x.ResourceId).NotEmpty().MaximumLength(500);
        RuleFor(x => x.Region).NotEmpty().MaximumLength(50);
        RuleFor(x => x.Slot).MaximumLength(100);
    }
}

public sealed class AddTargetHandler
{
    private readonly IEnvironmentRepository _environments;
    private readonly IUnitOfWork _uow;
    private readonly TimeProvider _clock;

    public AddTargetHandler(IEnvironmentRepository environments, IUnitOfWork uow, TimeProvider clock)
    {
        _environments = environments;
        _uow = uow;
        _clock = clock;
    }

    public async Task HandleAsync(AddTargetCommand cmd, CancellationToken cancellationToken = default)
    {
        var env = await _environments.GetByIdAsync(cmd.EnvironmentId, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Environment {cmd.EnvironmentId} not found.");

        env.AddTarget(cmd.TargetId, EnvironmentMapping.ToDomain(cmd.TargetKind),
            cmd.ResourceId, cmd.Region, cmd.Slot, _clock.GetUtcNow());

        await _uow.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}

// --- Update ---

public sealed record UpdateTargetCommand(
    Guid EnvironmentId,
    Guid TargetId,
    TargetKindDto TargetKind,
    string ResourceId,
    string Region,
    string? Slot);

public sealed class UpdateTargetValidator : AbstractValidator<UpdateTargetCommand>
{
    public UpdateTargetValidator()
    {
        RuleFor(x => x.EnvironmentId).NotEmpty();
        RuleFor(x => x.TargetId).NotEmpty();
        RuleFor(x => x.ResourceId).NotEmpty().MaximumLength(500);
        RuleFor(x => x.Region).NotEmpty().MaximumLength(50);
        RuleFor(x => x.Slot).MaximumLength(100);
    }
}

public sealed class UpdateTargetHandler
{
    private readonly IEnvironmentRepository _environments;
    private readonly IUnitOfWork _uow;
    private readonly TimeProvider _clock;

    public UpdateTargetHandler(IEnvironmentRepository environments, IUnitOfWork uow, TimeProvider clock)
    {
        _environments = environments;
        _uow = uow;
        _clock = clock;
    }

    public async Task HandleAsync(UpdateTargetCommand cmd, CancellationToken cancellationToken = default)
    {
        var env = await _environments.GetByIdAsync(cmd.EnvironmentId, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Environment {cmd.EnvironmentId} not found.");

        env.UpdateTarget(cmd.TargetId, EnvironmentMapping.ToDomain(cmd.TargetKind),
            cmd.ResourceId, cmd.Region, cmd.Slot, _clock.GetUtcNow());

        await _uow.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}

// --- Remove ---

public sealed record RemoveTargetCommand(Guid EnvironmentId, Guid TargetId);

public sealed class RemoveTargetValidator : AbstractValidator<RemoveTargetCommand>
{
    public RemoveTargetValidator()
    {
        RuleFor(x => x.EnvironmentId).NotEmpty();
        RuleFor(x => x.TargetId).NotEmpty();
    }
}

public sealed class RemoveTargetHandler
{
    private readonly IEnvironmentRepository _environments;
    private readonly IUnitOfWork _uow;
    private readonly TimeProvider _clock;

    public RemoveTargetHandler(IEnvironmentRepository environments, IUnitOfWork uow, TimeProvider clock)
    {
        _environments = environments;
        _uow = uow;
        _clock = clock;
    }

    public async Task HandleAsync(RemoveTargetCommand cmd, CancellationToken cancellationToken = default)
    {
        var env = await _environments.GetByIdAsync(cmd.EnvironmentId, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Environment {cmd.EnvironmentId} not found.");

        env.RemoveTarget(cmd.TargetId, _clock.GetUtcNow());
        await _uow.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
