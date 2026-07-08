using Microsoft.Extensions.Logging;
using Deployment.Application.Abstractions;
using Deployment.Domain.Abstractions;
using Deployment.Domain.Previews;
using Deployment.Domain.Previews.Events;
using Wolverine.Attributes;

namespace Deployment.Application.Features.Previews;

/// <summary>
/// Executes a requested preview environment by shelling out to Aspir8 via <see cref="IAspirateRunner"/> to
/// apply the app's manifest into the preview namespace, then settles it. Discovered by Wolverine as a handler
/// for <see cref="PreviewEnvironmentRequested"/>, so it runs asynchronously with the bus's retry + SQL outbox.
/// Mirrors <c>AspireApplicationRunExecutor</c>.
///
/// [WolverineHandler] is REQUIRED: Wolverine only auto-discovers types whose names end in "Handler"/"Consumer",
/// so a "*Executor" is invisible without it (the preview stays Creating).
/// </summary>
[WolverineHandler]
public sealed class PreviewEnvironmentDeployExecutor
{
    public async Task Handle(
        PreviewEnvironmentRequested evt,
        IPreviewEnvironmentRepository previews,
        IAspirateRunner aspirate,
        IUnitOfWork uow,
        TimeProvider clock,
        ILogger<PreviewEnvironmentDeployExecutor> logger,
        CancellationToken ct)
    {
        var preview = await previews.GetByIdAsync(evt.PreviewId, ct).ConfigureAwait(false);
        if (preview is null || preview.Status != PreviewStatus.Creating) return; // idempotent under retries

        AspirateDeployResult result;
        try
        {
            result = await aspirate.DeployAsync(
                new AspirateDeployRequest(preview.ManifestSource, preview.KubeContext, preview.Namespace), ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[preview] {Preview} threw.", preview.Id);
            result = new AspirateDeployResult(false, ex.ToString(), ex.Message);
        }

        var now = clock.GetUtcNow();
        if (result.Success) preview.MarkActive(result.Log, now);
        else preview.MarkFailed(result.FailureReason ?? "aspirate deploy failed.", result.Log, now);

        await uow.SaveChangesAsync(ct).ConfigureAwait(false);
        logger.LogInformation("[preview] {Preview} ({Namespace}) -> {Status}.", preview.Id, preview.Namespace, preview.Status);
    }
}
