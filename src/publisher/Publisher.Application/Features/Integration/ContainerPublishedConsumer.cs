using Microsoft.Extensions.Logging;
using Publisher.Application.Features.Containers;

namespace Publisher.Application.Features.Integration;

/// <summary>
/// Consumer edge (bus → publisher): handles the cross-service
/// <see cref="Cicd.IntegrationEvents.Ci.ContainerPublished"/> integration event from the
/// "ci.events" channel and records the container in the publisher's local Nexus inventory.
/// Idempotency is provided by the upsert in <see cref="RecordContainerHandler"/> (keyed on
/// repository + container name + version), backstopped by Wolverine's SQL inbox.
/// </summary>
public sealed class ContainerPublishedConsumer
{
    public async Task Handle(
        Cicd.IntegrationEvents.Ci.ContainerPublished evt,
        RecordContainerHandler recorder,
        ILogger<ContainerPublishedConsumer> logger,
        CancellationToken cancellationToken)
    {
        logger.LogInformation(
            "[bus] ContainerPublished received — build {Build}, repo {Repo}, container '{Container}' {Version}, uri {Uri}.",
            evt.BuildId, evt.RepositoryId, evt.ContainerName, evt.Version, evt.ArtifactUri);

        var id = await recorder.HandleAsync(
            new RecordContainerCommand(
                RepositoryId: evt.RepositoryId,
                BuildId: evt.BuildId,
                ContainerName: evt.ContainerName,
                Version: evt.Version,
                CommitSha: evt.CommitSha,
                ArtifactUri: evt.ArtifactUri,
                ObservedAtUtc: evt.OccurredAtUtc),
            cancellationToken).ConfigureAwait(false);

        logger.LogInformation("[bus] ContainerPublished recorded as inventory container {ContainerId}.", id);
    }
}
