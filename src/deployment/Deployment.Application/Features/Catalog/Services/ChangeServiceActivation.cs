using Deployment.Domain.Abstractions;
using Deployment.Domain.DeployableUnits;
using FluentValidation;

namespace Deployment.Application.Features.Catalog.Services;

/// <summary>
/// Toggle the IsActive flag. One command handles both directions to keep the
/// HTTP endpoint surface tight; idempotent on either side.
/// </summary>
public sealed record ChangeServiceActivationCommand(Guid ServiceId, bool Activate);

public sealed class ChangeServiceActivationValidator : AbstractValidator<ChangeServiceActivationCommand>
{
    public ChangeServiceActivationValidator()
    {
        RuleFor(x => x.ServiceId).NotEmpty();
    }
}

public sealed class ChangeServiceActivationHandler
{
    private readonly IServiceRepository _services;
    private readonly IUnitOfWork _uow;
    private readonly TimeProvider _clock;

    public ChangeServiceActivationHandler(IServiceRepository services, IUnitOfWork uow, TimeProvider clock)
    {
        _services = services;
        _uow = uow;
        _clock = clock;
    }

    public async Task HandleAsync(ChangeServiceActivationCommand cmd, CancellationToken cancellationToken = default)
    {
        var service = await _services.GetByIdAsync(cmd.ServiceId, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Service {cmd.ServiceId} not found.");

        var now = _clock.GetUtcNow();
        if (cmd.Activate) service.Reactivate(now);
        else service.Deactivate(now);

        await _uow.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
