using Deployment.Contracts.Catalog;
using Deployment.Domain.Abstractions;
using Deployment.Domain.ContainerImages;
using FluentValidation;

namespace Deployment.Application.Features.Catalog.ContainerImages;

/// <summary>
/// Register a container-image coordinate. Enforces the unique-coordinate invariant
/// (Registry + Repository + Name). The auto-upsert on release publish reuses the same
/// aggregate ctor via <see cref="ContainerImageUpserter"/>.
/// </summary>
public sealed record RegisterContainerImageCommand(
    Guid Id,
    string Registry,
    string Repository,
    string Name,
    string? DefaultTag);

public sealed class RegisterContainerImageValidator : AbstractValidator<RegisterContainerImageCommand>
{
    public RegisterContainerImageValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Registry).NotEmpty().MaximumLength(300);
        RuleFor(x => x.Repository).NotEmpty().MaximumLength(300);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(300);
        RuleFor(x => x.DefaultTag).MaximumLength(200);
    }
}

public sealed class RegisterContainerImageHandler
{
    private readonly IContainerImageRepository _images;
    private readonly IUnitOfWork _uow;
    private readonly TimeProvider _clock;

    public RegisterContainerImageHandler(IContainerImageRepository images, IUnitOfWork uow, TimeProvider clock)
    {
        _images = images;
        _uow = uow;
        _clock = clock;
    }

    public async Task<ContainerImageDto> HandleAsync(RegisterContainerImageCommand cmd, CancellationToken cancellationToken = default)
    {
        var existing = await _images.FindByCoordinateAsync(cmd.Registry, cmd.Repository, cmd.Name, cancellationToken).ConfigureAwait(false);
        if (existing is not null)
            throw new InvalidOperationException(
                $"A container image '{cmd.Registry}/{cmd.Repository}/{cmd.Name}' already exists.");

        var image = new ContainerImage(
            id: cmd.Id,
            registry: cmd.Registry,
            repository: cmd.Repository,
            name: cmd.Name,
            defaultTag: cmd.DefaultTag,
            createdAtUtc: _clock.GetUtcNow());

        await _images.AddAsync(image, cancellationToken).ConfigureAwait(false);
        await _uow.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return image.ToDto();
    }
}
