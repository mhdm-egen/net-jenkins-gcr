using Deployment.Contracts.Catalog;
using Deployment.Domain.Abstractions;
using Deployment.Domain.ContainerImages;
using FluentValidation;

namespace Deployment.Application.Features.Catalog.ContainerImages;

public sealed record ChangeContainerImageDefaultTagCommand(Guid Id, string DefaultTag);

public sealed class ChangeContainerImageDefaultTagValidator : AbstractValidator<ChangeContainerImageDefaultTagCommand>
{
    public ChangeContainerImageDefaultTagValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.DefaultTag).NotEmpty().MaximumLength(200);
    }
}

public sealed class ChangeContainerImageDefaultTagHandler
{
    private readonly IContainerImageRepository _images;
    private readonly IUnitOfWork _uow;
    private readonly TimeProvider _clock;

    public ChangeContainerImageDefaultTagHandler(IContainerImageRepository images, IUnitOfWork uow, TimeProvider clock)
    {
        _images = images;
        _uow = uow;
        _clock = clock;
    }

    public async Task<ContainerImageDto> HandleAsync(ChangeContainerImageDefaultTagCommand cmd, CancellationToken cancellationToken = default)
    {
        var image = await _images.GetByIdAsync(cmd.Id, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Container image {cmd.Id} not found.");

        image.ChangeDefaultTag(cmd.DefaultTag, _clock.GetUtcNow());
        await _uow.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return image.ToDto();
    }
}
