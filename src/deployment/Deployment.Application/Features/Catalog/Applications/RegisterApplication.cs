using Deployment.Contracts.Catalog;
using Deployment.Domain.Abstractions;
using Deployment.Domain.DeployableUnits;
using FluentValidation;
using DeployableApplication = Deployment.Domain.DeployableUnits.Application;

namespace Deployment.Application.Features.Catalog.Applications;

public sealed record RegisterApplicationCommand(
    Guid Id,
    string Name,
    string Description);

public sealed class RegisterApplicationValidator : AbstractValidator<RegisterApplicationCommand>
{
    public RegisterApplicationValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Description).NotNull().MaximumLength(1000);
    }
}

public sealed class RegisterApplicationHandler
{
    private readonly IApplicationRepository _apps;
    private readonly IUnitOfWork _uow;
    private readonly TimeProvider _clock;

    public RegisterApplicationHandler(IApplicationRepository apps, IUnitOfWork uow, TimeProvider clock)
    {
        _apps = apps;
        _uow = uow;
        _clock = clock;
    }

    public async Task<ApplicationDto> HandleAsync(RegisterApplicationCommand cmd, CancellationToken cancellationToken = default)
    {
        var existing = await _apps.FindByNameAsync(cmd.Name, cancellationToken).ConfigureAwait(false);
        if (existing is not null)
            throw new InvalidOperationException($"An application named '{cmd.Name}' already exists.");

        var app = new DeployableApplication(cmd.Id, cmd.Name, cmd.Description, _clock.GetUtcNow());
        await _apps.AddAsync(app, cancellationToken).ConfigureAwait(false);
        await _uow.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        // No members yet → empty name dictionary is fine.
        return app.ToDto(new Dictionary<Guid, string>());
    }
}
