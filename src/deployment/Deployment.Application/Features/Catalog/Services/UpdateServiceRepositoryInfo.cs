using Deployment.Domain.Abstractions;
using Deployment.Domain.DeployableUnits;
using FluentValidation;

namespace Deployment.Application.Features.Catalog.Services;

public sealed record UpdateServiceRepositoryInfoCommand(
    Guid ServiceId,
    string RepositoryUrl,
    string TargetFramework);

public sealed class UpdateServiceRepositoryInfoValidator : AbstractValidator<UpdateServiceRepositoryInfoCommand>
{
    public UpdateServiceRepositoryInfoValidator()
    {
        RuleFor(x => x.ServiceId).NotEmpty();
        RuleFor(x => x.RepositoryUrl).NotEmpty().MaximumLength(500);
        RuleFor(x => x.TargetFramework).NotEmpty().MaximumLength(50);
    }
}

public sealed class UpdateServiceRepositoryInfoHandler
{
    private readonly IServiceRepository _services;
    private readonly IUnitOfWork _uow;
    private readonly TimeProvider _clock;

    public UpdateServiceRepositoryInfoHandler(IServiceRepository services, IUnitOfWork uow, TimeProvider clock)
    {
        _services = services;
        _uow = uow;
        _clock = clock;
    }

    public async Task HandleAsync(UpdateServiceRepositoryInfoCommand cmd, CancellationToken cancellationToken = default)
    {
        var service = await _services.GetByIdAsync(cmd.ServiceId, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Service {cmd.ServiceId} not found.");

        service.UpdateRepositoryInfo(cmd.RepositoryUrl, cmd.TargetFramework, _clock.GetUtcNow());
        await _uow.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
