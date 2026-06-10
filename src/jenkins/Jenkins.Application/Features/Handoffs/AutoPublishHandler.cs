using Jenkins.Domain.Builds;
using Jenkins.Domain.Builds.Events;
using Jenkins.Domain.SourceRepositories;
using Microsoft.Extensions.Logging;

namespace Jenkins.Application.Features.Handoffs;

/// <summary>
/// Auto-publish wiring (decision #3): when a container's Nexus push is recorded, a
/// <see cref="ContainerPublished"/> domain event is raised. If the build has
/// succeeded and the matching <c>DeployableComponent.AutoPublish</c> is set, this
/// handler fires <see cref="PromoteToReleaseHandler"/> on its behalf — hands-off
/// promotion for repos that opted in.
///
/// Discovered and invoked by Wolverine off the in-process bus (the
/// <c>WolverineDomainEventDispatcher</c> publishes domain events here). Runs in its
/// own message scope. No-ops when the build isn't yet Succeeded or the component
/// isn't opted in, so it's safe regardless of build/publish ordering; promotion is
/// idempotent, so duplicate events don't double-publish.
/// </summary>
public sealed class AutoPublishHandler
{
    /// <summary>Principal recorded on auto-promoted handoffs.</summary>
    public const string AutoPrincipal = "ci-auto-publish";

    public async Task Handle(
        ContainerPublished evt,
        IBuildStore builds,
        ISourceRepositoryStore repositories,
        PromoteToReleaseHandler promote,
        ILogger<AutoPublishHandler> logger,
        CancellationToken cancellationToken)
    {
        var build = await builds.GetByIdAsync(evt.BuildId, cancellationToken).ConfigureAwait(false);
        if (build is null || build.Status != BuildStatus.Succeeded)
            return; // wait until the build has completed successfully

        var repository = await repositories.GetByIdAsync(build.RepositoryId, cancellationToken).ConfigureAwait(false);
        var component = repository?.MatchComponent(evt.ContainerName);
        if (component is null || !component.AutoPublish)
            return; // container isn't mapped or isn't opted into auto-publish

        logger.LogInformation(
            "[auto-publish] Promoting build {Build} artifact {Artifact} (container '{Container}').",
            evt.BuildId, evt.BuildArtifactId, evt.ContainerName);

        await promote
            .HandleAsync(new PromoteToReleaseCommand(evt.BuildId, evt.BuildArtifactId, AutoPrincipal), cancellationToken)
            .ConfigureAwait(false);
    }
}
