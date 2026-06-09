using Deployment.Domain.Abstractions;
using Deployment.Domain.Environments;
using FluentValidation;

namespace Deployment.Application.Features.Environments.EditEnvironment;

// --- Rename ---

public sealed record RenameEnvironmentCommand(Guid EnvironmentId, string NewName);

public sealed class RenameEnvironmentValidator : AbstractValidator<RenameEnvironmentCommand>
{
    public RenameEnvironmentValidator()
    {
        RuleFor(x => x.EnvironmentId).NotEmpty();
        RuleFor(x => x.NewName).NotEmpty().MaximumLength(100);
    }
}

public sealed class RenameEnvironmentHandler
{
    private readonly IEnvironmentRepository _environments;
    private readonly IUnitOfWork _uow;
    private readonly TimeProvider _clock;

    public RenameEnvironmentHandler(IEnvironmentRepository environments, IUnitOfWork uow, TimeProvider clock)
    {
        _environments = environments;
        _uow = uow;
        _clock = clock;
    }

    public async Task HandleAsync(RenameEnvironmentCommand cmd, CancellationToken cancellationToken = default)
    {
        var env = await _environments.GetByIdAsync(cmd.EnvironmentId, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Environment {cmd.EnvironmentId} not found.");

        if (!string.Equals(env.Name, cmd.NewName, StringComparison.Ordinal))
        {
            var clash = await _environments.FindByNameAsync(cmd.NewName, cancellationToken).ConfigureAwait(false);
            if (clash is not null && clash.Id != env.Id)
                throw new InvalidOperationException($"An environment named '{cmd.NewName}' already exists.");
        }

        env.Rename(cmd.NewName, _clock.GetUtcNow());
        await _uow.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}

// --- Change promotion rank ---

public sealed record ChangePromotionRankCommand(Guid EnvironmentId, int NewRank);

public sealed class ChangePromotionRankValidator : AbstractValidator<ChangePromotionRankCommand>
{
    public ChangePromotionRankValidator()
    {
        RuleFor(x => x.EnvironmentId).NotEmpty();
        RuleFor(x => x.NewRank).GreaterThanOrEqualTo(0);
    }
}

public sealed class ChangePromotionRankHandler
{
    private readonly IEnvironmentRepository _environments;
    private readonly IUnitOfWork _uow;
    private readonly TimeProvider _clock;

    public ChangePromotionRankHandler(IEnvironmentRepository environments, IUnitOfWork uow, TimeProvider clock)
    {
        _environments = environments;
        _uow = uow;
        _clock = clock;
    }

    public async Task HandleAsync(ChangePromotionRankCommand cmd, CancellationToken cancellationToken = default)
    {
        var env = await _environments.GetByIdAsync(cmd.EnvironmentId, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Environment {cmd.EnvironmentId} not found.");

        env.ChangePromotionRank(cmd.NewRank, _clock.GetUtcNow());
        await _uow.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}

// --- Set approval requirement ---

public sealed record SetApprovalRequirementCommand(Guid EnvironmentId, bool RequiresApproval);

public sealed class SetApprovalRequirementValidator : AbstractValidator<SetApprovalRequirementCommand>
{
    public SetApprovalRequirementValidator() => RuleFor(x => x.EnvironmentId).NotEmpty();
}

public sealed class SetApprovalRequirementHandler
{
    private readonly IEnvironmentRepository _environments;
    private readonly IUnitOfWork _uow;
    private readonly TimeProvider _clock;

    public SetApprovalRequirementHandler(IEnvironmentRepository environments, IUnitOfWork uow, TimeProvider clock)
    {
        _environments = environments;
        _uow = uow;
        _clock = clock;
    }

    public async Task HandleAsync(SetApprovalRequirementCommand cmd, CancellationToken cancellationToken = default)
    {
        var env = await _environments.GetByIdAsync(cmd.EnvironmentId, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Environment {cmd.EnvironmentId} not found.");

        env.SetApprovalRequirement(cmd.RequiresApproval, _clock.GetUtcNow());
        await _uow.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}

// --- Set production flag ---

public sealed record SetProductionFlagCommand(Guid EnvironmentId, bool IsProduction);

public sealed class SetProductionFlagValidator : AbstractValidator<SetProductionFlagCommand>
{
    public SetProductionFlagValidator() => RuleFor(x => x.EnvironmentId).NotEmpty();
}

public sealed class SetProductionFlagHandler
{
    private readonly IEnvironmentRepository _environments;
    private readonly IUnitOfWork _uow;
    private readonly TimeProvider _clock;

    public SetProductionFlagHandler(IEnvironmentRepository environments, IUnitOfWork uow, TimeProvider clock)
    {
        _environments = environments;
        _uow = uow;
        _clock = clock;
    }

    public async Task HandleAsync(SetProductionFlagCommand cmd, CancellationToken cancellationToken = default)
    {
        var env = await _environments.GetByIdAsync(cmd.EnvironmentId, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Environment {cmd.EnvironmentId} not found.");

        env.SetProductionFlag(cmd.IsProduction, _clock.GetUtcNow());
        await _uow.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
