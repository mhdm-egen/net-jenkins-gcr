using Deployment.Domain.Abstractions;
using Deployment.Domain.DeployableUnits;
using FluentValidation;

namespace Deployment.Application.Features.Catalog.Applications;

public sealed record RenameApplicationCommand(Guid ApplicationId, string NewName);

public sealed class RenameApplicationValidator : AbstractValidator<RenameApplicationCommand>
{
    public RenameApplicationValidator()
    {
        RuleFor(x => x.ApplicationId).NotEmpty();
        RuleFor(x => x.NewName).NotEmpty().MaximumLength(200);
    }
}

public sealed class RenameApplicationHandler
{
    private readonly IApplicationRepository _apps;
    private readonly IUnitOfWork _uow;
    private readonly TimeProvider _clock;

    public RenameApplicationHandler(IApplicationRepository apps, IUnitOfWork uow, TimeProvider clock)
    {
        _apps = apps;
        _uow = uow;
        _clock = clock;
    }

    public async Task HandleAsync(RenameApplicationCommand cmd, CancellationToken cancellationToken = default)
    {
        var app = await _apps.GetByIdAsync(cmd.ApplicationId, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Application {cmd.ApplicationId} not found.");

        if (!string.Equals(app.Name, cmd.NewName, StringComparison.Ordinal))
        {
            var clash = await _apps.FindByNameAsync(cmd.NewName, cancellationToken).ConfigureAwait(false);
            if (clash is not null && clash.Id != app.Id)
                throw new InvalidOperationException($"An application named '{cmd.NewName}' already exists.");
        }

        app.Rename(cmd.NewName, _clock.GetUtcNow());
        await _uow.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}

public sealed record ChangeApplicationDescriptionCommand(Guid ApplicationId, string Description);

public sealed class ChangeApplicationDescriptionValidator : AbstractValidator<ChangeApplicationDescriptionCommand>
{
    public ChangeApplicationDescriptionValidator()
    {
        RuleFor(x => x.ApplicationId).NotEmpty();
        RuleFor(x => x.Description).NotNull().MaximumLength(1000);
    }
}

public sealed class ChangeApplicationDescriptionHandler
{
    private readonly IApplicationRepository _apps;
    private readonly IUnitOfWork _uow;
    private readonly TimeProvider _clock;

    public ChangeApplicationDescriptionHandler(IApplicationRepository apps, IUnitOfWork uow, TimeProvider clock)
    {
        _apps = apps;
        _uow = uow;
        _clock = clock;
    }

    public async Task HandleAsync(ChangeApplicationDescriptionCommand cmd, CancellationToken cancellationToken = default)
    {
        var app = await _apps.GetByIdAsync(cmd.ApplicationId, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Application {cmd.ApplicationId} not found.");

        app.ChangeDescription(cmd.Description, _clock.GetUtcNow());
        await _uow.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}

public sealed record ChangeApplicationActivationCommand(Guid ApplicationId, bool Activate);

public sealed class ChangeApplicationActivationValidator : AbstractValidator<ChangeApplicationActivationCommand>
{
    public ChangeApplicationActivationValidator()
    {
        RuleFor(x => x.ApplicationId).NotEmpty();
    }
}

public sealed class ChangeApplicationActivationHandler
{
    private readonly IApplicationRepository _apps;
    private readonly IUnitOfWork _uow;
    private readonly TimeProvider _clock;

    public ChangeApplicationActivationHandler(IApplicationRepository apps, IUnitOfWork uow, TimeProvider clock)
    {
        _apps = apps;
        _uow = uow;
        _clock = clock;
    }

    public async Task HandleAsync(ChangeApplicationActivationCommand cmd, CancellationToken cancellationToken = default)
    {
        var app = await _apps.GetByIdAsync(cmd.ApplicationId, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Application {cmd.ApplicationId} not found.");

        var now = _clock.GetUtcNow();
        if (cmd.Activate) app.Reactivate(now);
        else app.Deactivate(now);

        await _uow.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
