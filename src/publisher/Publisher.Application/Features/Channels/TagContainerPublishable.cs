using FluentValidation;
using Publisher.Domain.Abstractions;
using Publisher.Domain.Channels;
using Publisher.Domain.Containers;

namespace Publisher.Application.Features.Channels;

/// <summary>
/// Tags a specific inventory container as publishable under a channel name. Creates the channel
/// on first use; otherwise moves the channel's pointer to the new container (mutable alias).
/// </summary>
public sealed record TagContainerPublishableCommand(string ChannelName, Guid ContainerId, string? BoundBy);

public sealed class TagContainerPublishableValidator : AbstractValidator<TagContainerPublishableCommand>
{
    public TagContainerPublishableValidator()
    {
        RuleFor(x => x.ChannelName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.ContainerId).NotEmpty();
    }
}

public sealed class TagContainerPublishableHandler
{
    private readonly IPublishChannelRepository _channels;
    private readonly IPublishableContainerRepository _containers;
    private readonly IUnitOfWork _uow;
    private readonly TimeProvider _clock;

    public TagContainerPublishableHandler(
        IPublishChannelRepository channels,
        IPublishableContainerRepository containers,
        IUnitOfWork uow,
        TimeProvider clock)
    {
        _channels = channels;
        _containers = containers;
        _uow = uow;
        _clock = clock;
    }

    public async Task<Guid> HandleAsync(TagContainerPublishableCommand cmd, CancellationToken cancellationToken = default)
    {
        // The container must exist in inventory before it can be made publishable.
        var container = await _containers.GetByIdAsync(cmd.ContainerId, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Container {cmd.ContainerId} is not in the publisher inventory.");

        var now = _clock.GetUtcNow();
        var boundBy = string.IsNullOrWhiteSpace(cmd.BoundBy) ? "system" : cmd.BoundBy!.Trim();

        var channel = await _channels.FindByNameAsync(cmd.ChannelName, cancellationToken).ConfigureAwait(false);
        if (channel is null)
        {
            channel = PublishChannel.Create(Guid.NewGuid(), cmd.ChannelName, container.Id, boundBy, now);
            await _channels.AddAsync(channel, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            channel.Bind(container.Id, boundBy, now);
        }

        await _uow.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return channel.Id;
    }
}
