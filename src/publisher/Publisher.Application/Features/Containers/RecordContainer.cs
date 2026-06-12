using Publisher.Domain.Abstractions;
using Publisher.Domain.Containers;

namespace Publisher.Application.Features.Containers;

/// <summary>
/// Upserts a container into the publisher's inventory. Driven by the CI
/// <c>ContainerPublished</c> bus consumer (and idempotent because that upstream event is
/// poll-driven and can repeat): a first sighting creates the record; a re-sighting refreshes
/// its reference/digest and last-seen timestamp.
/// </summary>
public sealed record RecordContainerCommand(
    Guid RepositoryId,
    Guid BuildId,
    string ContainerName,
    string Version,
    string CommitSha,
    string ArtifactUri,
    DateTimeOffset ObservedAtUtc);

public sealed class RecordContainerHandler
{
    private readonly IPublishableContainerRepository _containers;
    private readonly IUnitOfWork _uow;

    public RecordContainerHandler(IPublishableContainerRepository containers, IUnitOfWork uow)
    {
        _containers = containers;
        _uow = uow;
    }

    public async Task<Guid> HandleAsync(RecordContainerCommand cmd, CancellationToken cancellationToken = default)
    {
        var existing = await _containers
            .FindByNaturalKeyAsync(cmd.RepositoryId, cmd.ContainerName, cmd.Version, cancellationToken)
            .ConfigureAwait(false);

        if (existing is not null)
        {
            existing.Reobserve(cmd.BuildId, cmd.CommitSha, cmd.ArtifactUri, cmd.ObservedAtUtc);
            await _uow.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return existing.Id;
        }

        var container = new PublishableContainer(
            id: Guid.NewGuid(),
            repositoryId: cmd.RepositoryId,
            buildId: cmd.BuildId,
            containerName: cmd.ContainerName,
            version: cmd.Version,
            commitSha: cmd.CommitSha,
            artifactUri: cmd.ArtifactUri,
            observedAtUtc: cmd.ObservedAtUtc);

        await _containers.AddAsync(container, cancellationToken).ConfigureAwait(false);
        await _uow.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return container.Id;
    }
}
