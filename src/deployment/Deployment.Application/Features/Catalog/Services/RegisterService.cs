using Deployment.Contracts.Catalog;
using Deployment.Domain.Abstractions;
using Deployment.Domain.DeployableUnits;
using FluentValidation;

namespace Deployment.Application.Features.Catalog.Services;

public sealed record RegisterServiceCommand(
    Guid Id,
    string Name,
    ServiceKindDto Kind,
    string RepositoryUrl,
    string TargetFramework);

public sealed class RegisterServiceValidator : AbstractValidator<RegisterServiceCommand>
{
    public RegisterServiceValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.RepositoryUrl).NotEmpty().MaximumLength(500);
        RuleFor(x => x.TargetFramework).NotEmpty().MaximumLength(50);
    }
}

public sealed class RegisterServiceHandler
{
    private readonly IServiceRepository _services;
    private readonly IUnitOfWork _uow;
    private readonly TimeProvider _clock;

    public RegisterServiceHandler(IServiceRepository services, IUnitOfWork uow, TimeProvider clock)
    {
        _services = services;
        _uow = uow;
        _clock = clock;
    }

    public async Task<ServiceDto> HandleAsync(RegisterServiceCommand cmd, CancellationToken cancellationToken = default)
    {
        var existing = await _services.FindByNameAsync(cmd.Name, cancellationToken).ConfigureAwait(false);
        if (existing is not null)
            throw new InvalidOperationException($"A service named '{cmd.Name}' already exists.");

        var service = new Service(
            id: cmd.Id,
            name: cmd.Name,
            kind: cmd.Kind.ToDomain(),
            repositoryUrl: cmd.RepositoryUrl,
            targetFramework: cmd.TargetFramework,
            registeredAtUtc: _clock.GetUtcNow());

        await _services.AddAsync(service, cancellationToken).ConfigureAwait(false);
        await _uow.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return service.ToDto();
    }
}
