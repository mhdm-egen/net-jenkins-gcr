using Microsoft.Extensions.Logging;
using Deployment.Application.Features.AspireApps;
using Deployment.Application.Features.Previews;
using Deployment.Domain.Abstractions;
using Deployment.Domain.AspireApps;

namespace Deployment.Application.Features.Integration;

/// <summary>
/// Consumer edge (bus → deployment): handles <see cref="Cicd.IntegrationEvents.Ci.AspireAppPublished"/>
/// from "ci.events". Resolves the registered <see cref="AspireApplication"/> by its explicit
/// <c>SourceKey</c> (falling back to name-matching), then routes by branch: a publish on the app's
/// <c>MainBranch</c> refreshes its <c>ManifestSource</c>/<c>Version</c> and (if <c>AutoDeploy</c>) deploys to
/// its environment; a publish on any other branch stands up / refreshes an ephemeral preview environment
/// keyed by that branch. <b>Update-only</b>: a new app must first be registered in the UI (its Kubernetes
/// environment is a human choice), so an unmatched publish is a no-op. Idempotency: Wolverine's SQL inbox +
/// the domain's no-op on an unchanged manifest.
/// </summary>
public sealed class AspireAppPublishedConsumer
{
    public async Task Handle(
        Cicd.IntegrationEvents.Ci.AspireAppPublished evt,
        IAspireApplicationRepository apps,
        RequestAspireDeploymentHandler request,
        CreatePreviewEnvironmentHandler createPreview,
        IUnitOfWork uow,
        TimeProvider clock,
        ILogger<AspireAppPublishedConsumer> logger,
        CancellationToken ct)
    {
        var app = await apps.FindBySourceKeyAsync(evt.AppName, ct).ConfigureAwait(false);
        if (app is null)
        {
            logger.LogInformation(
                "[bus] AspireAppPublished '{App}' {Version} -> no registered Aspire application tracks that source; ignored " +
                "(register one with a matching name or source key + a Kubernetes environment to enable the handoff).",
                evt.AppName, evt.Version);
            return;
        }

        // Non-main branch → route to an ephemeral preview environment keyed by the branch (create or refresh).
        if (!app.IsMainBranch(evt.Branch))
        {
            var preview = await createPreview.HandleAsync(new CreatePreviewEnvironmentCommand(
                app.Id, evt.Branch, evt.ManifestUrl, evt.Version, TtlHours: null, TriggeredBy: $"ci:{evt.CommitSha}"), ct)
                .ConfigureAwait(false);
            logger.LogInformation(
                "[bus] AspireAppPublished '{App}' {Version} on branch '{Branch}' -> preview {Namespace} ({Outcome}).",
                evt.AppName, evt.Version, evt.Branch, preview.Namespace, preview.Outcome);
            return;
        }

        var changed = app.ApplyPublishedManifest(evt.ManifestUrl, evt.Version, clock.GetUtcNow());
        await uow.SaveChangesAsync(ct).ConfigureAwait(false);

        if (!changed)
        {
            logger.LogInformation(
                "[bus] AspireAppPublished '{App}' {Version} -> manifest unchanged; no deployment requested.",
                evt.AppName, evt.Version);
            return;
        }

        if (!app.AutoDeploy)
        {
            logger.LogInformation(
                "[bus] AspireAppPublished '{App}' {Version} -> manifest updated; auto-deploy off (manual deploy).",
                evt.AppName, evt.Version);
            return;
        }

        var result = await request.HandleAsync(
            new RequestAspireDeploymentCommand(app.Id, $"auto:ci:{evt.CommitSha}"), ct).ConfigureAwait(false);

        logger.LogInformation(
            "[bus] AspireAppPublished '{App}' {Version} -> manifest updated + auto-deploy requested (run {Run}, outcome {Outcome}).",
            evt.AppName, evt.Version, result.RunId, result.Outcome);
    }
}
