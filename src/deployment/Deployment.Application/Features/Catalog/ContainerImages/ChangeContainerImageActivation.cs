using Deployment.Contracts.Catalog;
using Deployment.Domain.Abstractions;
using Deployment.Domain.ContainerImages;
using FluentValidation;

namespace Deployment.Application.Features.Catalog.ContainerImages;

/// <summary>
/// Activate / deactivate a coordinate. Deactivating hides it from pickers and discovery
/// without affecting existing releases (they hold their own resolved digest). Idempotent.
/// </summary>
public sealed record ChangeContainerImageActivationCommand(Guid Id, bool Activate);

public sealed class ChangeContainerImageActivationValidator : AbstractValidator<ChangeContainerImageActivationCommand>
{
    public ChangeContainerImageActivationValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
    }
}

public sealed class ChangeContainerImageActivationHandler
{
    private readonly IContainerImageRepository _images;
    private readonly IUnitOfWork _uow;
    private readonly TimeProvider _clock;

    public ChangeContainerImageActivationHandler(IContainerImageRepository images, IUnitOfWork uow, TimeProvider clock)
    {
        _images = images;
        _uow = uow;
        _clock = clock;
    }

    public async Task<ContainerImageDto> HandleAsync(ChangeContainerImageActivationCommand cmd, CancellationToken cancellationToken = default)
    {
        var image = await _images.GetByIdAsync(cmd.Id, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Container image {cmd.Id} not found.");

        var now = _clock.GetUtcNow();
        if (cmd.Activate) image.Reactivate(now);
        else image.Deactivate(now);

        await _uow.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return image.ToDto();
    }
}
