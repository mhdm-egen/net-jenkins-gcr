using Deployment.Domain.Abstractions;
using Deployment.Domain.DeployableUnits;
using FluentValidation;

namespace Deployment.Application.Features.Catalog.Applications;

public sealed record AddApplicationMemberCommand(
    Guid ApplicationId,
    Guid ServiceId,
    string Role,
    bool IsOptional,
    int DeploymentOrder);

public sealed class AddApplicationMemberValidator : AbstractValidator<AddApplicationMemberCommand>
{
    public AddApplicationMemberValidator()
    {
        RuleFor(x => x.ApplicationId).NotEmpty();
        RuleFor(x => x.ServiceId).NotEmpty();
        RuleFor(x => x.Role).NotEmpty().MaximumLength(100);
        RuleFor(x => x.DeploymentOrder).GreaterThanOrEqualTo(0);
    }
}

public sealed class AddApplicationMemberHandler
{
    private readonly IApplicationRepository _apps;
    private readonly IServiceRepository _services;
    private readonly IUnitOfWork _uow;
    private readonly TimeProvider _clock;

    public AddApplicationMemberHandler(
        IApplicationRepository apps,
        IServiceRepository services,
        IUnitOfWork uow,
        TimeProvider clock)
    {
        _apps = apps;
        _services = services;
        _uow = uow;
        _clock = clock;
    }

    public async Task HandleAsync(AddApplicationMemberCommand cmd, CancellationToken cancellationToken = default)
    {
        var app = await _apps.GetByIdAsync(cmd.ApplicationId, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Application {cmd.ApplicationId} not found.");

        var service = await _services.GetByIdAsync(cmd.ServiceId, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Service {cmd.ServiceId} not found.");

        app.AddService(service.Id, cmd.Role, cmd.IsOptional, cmd.DeploymentOrder, _clock.GetUtcNow());
        await _uow.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}

public sealed record UpdateApplicationMemberCommand(
    Guid ApplicationId,
    Guid ServiceId,
    string Role,
    bool IsOptional,
    int DeploymentOrder);

public sealed class UpdateApplicationMemberValidator : AbstractValidator<UpdateApplicationMemberCommand>
{
    public UpdateApplicationMemberValidator()
    {
        RuleFor(x => x.ApplicationId).NotEmpty();
        RuleFor(x => x.ServiceId).NotEmpty();
        RuleFor(x => x.Role).NotEmpty().MaximumLength(100);
        RuleFor(x => x.DeploymentOrder).GreaterThanOrEqualTo(0);
    }
}

public sealed class UpdateApplicationMemberHandler
{
    private readonly IApplicationRepository _apps;
    private readonly IUnitOfWork _uow;
    private readonly TimeProvider _clock;

    public UpdateApplicationMemberHandler(IApplicationRepository apps, IUnitOfWork uow, TimeProvider clock)
    {
        _apps = apps;
        _uow = uow;
        _clock = clock;
    }

    public async Task HandleAsync(UpdateApplicationMemberCommand cmd, CancellationToken cancellationToken = default)
    {
        var app = await _apps.GetByIdAsync(cmd.ApplicationId, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Application {cmd.ApplicationId} not found.");

        app.UpdateMembership(cmd.ServiceId, cmd.Role, cmd.IsOptional, cmd.DeploymentOrder, _clock.GetUtcNow());
        await _uow.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}

public sealed record RemoveApplicationMemberCommand(Guid ApplicationId, Guid ServiceId);

public sealed class RemoveApplicationMemberValidator : AbstractValidator<RemoveApplicationMemberCommand>
{
    public RemoveApplicationMemberValidator()
    {
        RuleFor(x => x.ApplicationId).NotEmpty();
        RuleFor(x => x.ServiceId).NotEmpty();
    }
}

public sealed class RemoveApplicationMemberHandler
{
    private readonly IApplicationRepository _apps;
    private readonly IUnitOfWork _uow;
    private readonly TimeProvider _clock;

    public RemoveApplicationMemberHandler(IApplicationRepository apps, IUnitOfWork uow, TimeProvider clock)
    {
        _apps = apps;
        _uow = uow;
        _clock = clock;
    }

    public async Task HandleAsync(RemoveApplicationMemberCommand cmd, CancellationToken cancellationToken = default)
    {
        var app = await _apps.GetByIdAsync(cmd.ApplicationId, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Application {cmd.ApplicationId} not found.");

        app.RemoveService(cmd.ServiceId, _clock.GetUtcNow());
        await _uow.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
