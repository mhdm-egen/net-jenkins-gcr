using FluentValidation;
using Publisher.Contracts.Containers;
using Publisher.Domain.Abstractions;
using Publisher.Domain.Containers;

namespace Publisher.Application.Features.Containers;

/// <summary>
/// Manually adds a container to the inventory (picked from the local Nexus docker registry in the
/// UI). The record is created active. Idempotent: if a record with the same natural key already
/// exists it is refreshed rather than duplicated.
/// </summary>
public sealed record AddContainerManuallyCommand(
    string ContainerName,
    string Version,
    string? CommitSha,
    string ArtifactUri);

public sealed class AddContainerManuallyValidator : AbstractValidator<AddContainerManuallyCommand>
{
    public AddContainerManuallyValidator()
    {
        RuleFor(x => x.ContainerName).NotEmpty().MaximumLength(300);
        RuleFor(x => x.Version).NotEmpty().MaximumLength(200);
        RuleFor(x => x.ArtifactUri).NotEmpty().MaximumLength(1000);
    }
}

public sealed class AddContainerManuallyHandler
{
    private readonly IPublishableContainerRepository _containers;
    private readonly IUnitOfWork _uow;
    private readonly TimeProvider _clock;

    public AddContainerManuallyHandler(IPublishableContainerRepository containers, IUnitOfWork uow, TimeProvider clock)
    {
        _containers = containers;
        _uow = uow;
        _clock = clock;
    }

    public async Task<Guid> HandleAsync(AddContainerManuallyCommand cmd, CancellationToken cancellationToken = default)
    {
        var now = _clock.GetUtcNow();

        // Manual records have no CI repository/build ids, so the natural key is (Empty, name, version).
        var existing = await _containers
            .FindByNaturalKeyAsync(Guid.Empty, cmd.ContainerName, cmd.Version, cancellationToken)
            .ConfigureAwait(false);

        if (existing is not null)
        {
            existing.Reobserve(Guid.Empty, cmd.CommitSha ?? string.Empty, cmd.ArtifactUri, now);
            existing.Reactivate(now);
            await _uow.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return existing.Id;
        }

        var container = new PublishableContainer(
            id: Guid.NewGuid(),
            repositoryId: Guid.Empty,
            buildId: Guid.Empty,
            containerName: cmd.ContainerName,
            version: cmd.Version,
            commitSha: cmd.CommitSha ?? string.Empty,
            artifactUri: cmd.ArtifactUri,
            observedAtUtc: now,
            source: ContainerSource.Manual);

        await _containers.AddAsync(container, cancellationToken).ConfigureAwait(false);
        await _uow.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return container.Id;
    }
}

// ---- Activation -------------------------------------------------------------------------------

public sealed record ChangeContainerActivationCommand(Guid ContainerId, bool Active);

public sealed class ChangeContainerActivationHandler
{
    private readonly IPublishableContainerRepository _containers;
    private readonly IUnitOfWork _uow;
    private readonly TimeProvider _clock;

    public ChangeContainerActivationHandler(IPublishableContainerRepository containers, IUnitOfWork uow, TimeProvider clock)
    {
        _containers = containers;
        _uow = uow;
        _clock = clock;
    }

    public async Task HandleAsync(ChangeContainerActivationCommand cmd, CancellationToken cancellationToken = default)
    {
        var container = await _containers.GetByIdAsync(cmd.ContainerId, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Container {cmd.ContainerId} not found.");

        var now = _clock.GetUtcNow();
        if (cmd.Active) container.Reactivate(now); else container.Deactivate(now);
        await _uow.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
