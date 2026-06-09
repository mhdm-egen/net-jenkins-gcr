using Deployment.Domain.Abstractions;
using Deployment.Domain.DeployableUnits;
using FluentValidation;

namespace Deployment.Application.Features.Catalog.Services;

public sealed record RenameServiceCommand(Guid ServiceId, string NewName);

public sealed class RenameServiceValidator : AbstractValidator<RenameServiceCommand>
{
    public RenameServiceValidator()
    {
        RuleFor(x => x.ServiceId).NotEmpty();
        RuleFor(x => x.NewName).NotEmpty().MaximumLength(200);
    }
}

public sealed class RenameServiceHandler
{
    private readonly IServiceRepository _services;
    private readonly IUnitOfWork _uow;
    private readonly TimeProvider _clock;

    public RenameServiceHandler(IServiceRepository services, IUnitOfWork uow, TimeProvider clock)
    {
        _services = services;
        _uow = uow;
        _clock = clock;
    }

    public async Task HandleAsync(RenameServiceCommand cmd, CancellationToken cancellationToken = default)
    {
        var service = await _services.GetByIdAsync(cmd.ServiceId, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Service {cmd.ServiceId} not found.");

        // Uniqueness pre-check — domain Rename is idempotent on same-name but
        // doesn't know about other rows.
        if (!string.Equals(service.Name, cmd.NewName, StringComparison.Ordinal))
        {
            var clash = await _services.FindByNameAsync(cmd.NewName, cancellationToken).ConfigureAwait(false);
            if (clash is not null && clash.Id != service.Id)
                throw new InvalidOperationException($"A service named '{cmd.NewName}' already exists.");
        }

        service.Rename(cmd.NewName, _clock.GetUtcNow());
        await _uow.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
