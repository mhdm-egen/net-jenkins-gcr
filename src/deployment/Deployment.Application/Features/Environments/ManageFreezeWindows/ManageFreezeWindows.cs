using Deployment.Domain.Abstractions;
using Deployment.Domain.Environments;
using FluentValidation;

namespace Deployment.Application.Features.Environments.ManageFreezeWindows;

// --- Schedule ---

public sealed record ScheduleFreezeWindowCommand(
    Guid EnvironmentId,
    Guid FreezeWindowId,
    DateTimeOffset StartUtc,
    DateTimeOffset EndUtc,
    string Reason,
    string CreatedByPrincipal);

public sealed class ScheduleFreezeWindowValidator : AbstractValidator<ScheduleFreezeWindowCommand>
{
    public ScheduleFreezeWindowValidator()
    {
        RuleFor(x => x.EnvironmentId).NotEmpty();
        RuleFor(x => x.FreezeWindowId).NotEmpty();
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(500);
        RuleFor(x => x.CreatedByPrincipal).NotEmpty().MaximumLength(200);
        RuleFor(x => x)
            .Must(x => x.EndUtc > x.StartUtc)
            .WithMessage("EndUtc must be after StartUtc.");
    }
}

public sealed class ScheduleFreezeWindowHandler
{
    private readonly IEnvironmentRepository _environments;
    private readonly IUnitOfWork _uow;
    private readonly TimeProvider _clock;

    public ScheduleFreezeWindowHandler(IEnvironmentRepository environments, IUnitOfWork uow, TimeProvider clock)
    {
        _environments = environments;
        _uow = uow;
        _clock = clock;
    }

    public async Task HandleAsync(ScheduleFreezeWindowCommand cmd, CancellationToken cancellationToken = default)
    {
        var env = await _environments.GetByIdAsync(cmd.EnvironmentId, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Environment {cmd.EnvironmentId} not found.");

        env.ScheduleFreezeWindow(cmd.FreezeWindowId, cmd.StartUtc, cmd.EndUtc,
            cmd.Reason, cmd.CreatedByPrincipal, _clock.GetUtcNow());

        await _uow.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}

// --- Cancel ---

public sealed record CancelFreezeWindowCommand(Guid EnvironmentId, Guid FreezeWindowId);

public sealed class CancelFreezeWindowValidator : AbstractValidator<CancelFreezeWindowCommand>
{
    public CancelFreezeWindowValidator()
    {
        RuleFor(x => x.EnvironmentId).NotEmpty();
        RuleFor(x => x.FreezeWindowId).NotEmpty();
    }
}

public sealed class CancelFreezeWindowHandler
{
    private readonly IEnvironmentRepository _environments;
    private readonly IUnitOfWork _uow;
    private readonly TimeProvider _clock;

    public CancelFreezeWindowHandler(IEnvironmentRepository environments, IUnitOfWork uow, TimeProvider clock)
    {
        _environments = environments;
        _uow = uow;
        _clock = clock;
    }

    public async Task HandleAsync(CancelFreezeWindowCommand cmd, CancellationToken cancellationToken = default)
    {
        var env = await _environments.GetByIdAsync(cmd.EnvironmentId, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Environment {cmd.EnvironmentId} not found.");

        env.CancelFreezeWindow(cmd.FreezeWindowId, _clock.GetUtcNow());
        await _uow.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
