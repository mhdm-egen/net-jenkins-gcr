using Microsoft.Extensions.Logging;
using Deployment.Application.Abstractions;
using Deployment.Application.Features.AspireApps;
using Deployment.Domain.Abstractions;
using Deployment.Domain.AspireApps;
using Deployment.Domain.AspireApps.Runs;
using Deployment.Domain.AspireApps.Runs.Events;
using Deployment.Domain.Runs;
using Wolverine.Attributes;

namespace Deployment.Application.Features.AspireApps;

/// <summary>
/// Executes a requested Aspire-application deployment by shelling out to Aspir8 via
/// <see cref="IAspirateRunner"/>, then settles the run. For a blue-green app it deploys to a parallel
/// <c>{namespace}-green</c> namespace, health-gates the whole app there, then (automatic or manual)
/// promotes by making green the app's active slot and retiring the old namespace. Discovered by Wolverine
/// as a handler for <see cref="AspireApplicationRunRequested"/>.
///
/// [WolverineHandler] is REQUIRED: Wolverine only auto-discovers types whose names end in
/// "Handler"/"Consumer", so a "*Executor" is invisible without it (the run stays Pending).
/// </summary>
[WolverineHandler]
public sealed class AspireApplicationRunExecutor
{
    public async Task Handle(
        AspireApplicationRunRequested evt,
        IAspireApplicationRunRepository runs,
        IAspireApplicationRepository apps,
        IAspirateRunner aspirate,
        IAspireClusterStatusReader clusterStatus,
        INamespaceManager namespaces,
        IIngressManager ingress,
        Observability.DeploymentTelemetry telemetry,
        IUnitOfWork uow,
        TimeProvider clock,
        ILogger<AspireApplicationRunExecutor> logger,
        CancellationToken ct)
    {
        var run = await runs.GetByIdAsync(evt.RunId, ct).ConfigureAwait(false);
        if (run is null || run.Status != DeploymentRunStatus.Pending) return; // idempotent under retries

        using var activity = Observability.DeploymentTelemetry.Activity.StartActivity("deploy.aspire");
        activity?.SetTag("deploy.app", run.ApplicationName);

        run.Start();

        var blueGreen = !string.IsNullOrWhiteSpace(run.RolloutGreenSlot);
        var targetNamespace = blueGreen ? $"{run.Namespace}-{run.RolloutGreenSlot}" : run.Namespace;

        AspirateDeployResult result;
        try
        {
            result = await aspirate.DeployAsync(
                new AspirateDeployRequest(run.ManifestSource, run.KubeContext, targetNamespace), ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[aspire] Run {Run} threw.", run.Id);
            result = new AspirateDeployResult(false, ex.ToString(), ex.Message);
        }

        var now = clock.GetUtcNow();
        if (!result.Success)
        {
            run.Fail(result.FailureReason ?? "aspirate deploy failed.", result.Log, now);
        }
        else if (!blueGreen)
        {
            var images = await SnapshotImagesAsync(clusterStatus, run.KubeContext, targetNamespace, run.Id, logger, ct).ConfigureAwait(false);
            // Stamp a browsable Ingress for the app's frontend (best-effort; host = {namespace}.{app-domain}).
            try { await ingress.EnsureAppIngressAsync(run.KubeContext, targetNamespace, targetNamespace, ct).ConfigureAwait(false); }
            catch (Exception ex) { logger.LogWarning(ex, "[aspire] Run {Run} ingress stamp failed; app reachable via port-forward.", run.Id); }
            run.Succeed(result.Log, images, now);
        }
        else
        {
            // Once green is on the cluster we MUST finish settling (promote or roll it back) — a half-settled
            // run leaks the green namespace. The gate can outlast a cancellation (shutdown, Wolverine timeout),
            // so settle + persist on a non-cancellable token; the gate self-bounds its own wall-clock.
            await SettleBlueGreenAsync(run, apps, clusterStatus, namespaces, targetNamespace, result.Log, now, logger, CancellationToken.None).ConfigureAwait(false);
        }

        await uow.SaveChangesAsync(blueGreen ? CancellationToken.None : ct).ConfigureAwait(false);
        activity?.SetTag("deploy.outcome", run.Status.ToString());
        if (run.Status is not DeploymentRunStatus.AwaitingPromotion)
            telemetry.RecordRun("aspire", run.Status.ToString(), blueGreen ? "BlueGreen" : "Direct", (now - run.RequestedAtUtc).TotalSeconds);
        logger.LogInformation("[aspire] Run {Run} -> {Status}.", run.Id, run.Status);
    }

    private async Task SettleBlueGreenAsync(
        AspireApplicationRun run, IAspireApplicationRepository apps, IAspireClusterStatusReader clusterStatus,
        INamespaceManager namespaces, string greenNamespace, string? log, DateTimeOffset now, ILogger logger, CancellationToken ct)
    {
        var app = await apps.GetByIdAsync(run.ApplicationId, ct).ConfigureAwait(false);
        var greenSlot = run.RolloutGreenSlot!;
        var activeSlot = run.RolloutActiveSlot; // null on bootstrap

        var healthy = await HealthGateAsync(clusterStatus, run.KubeContext, greenNamespace, ct).ConfigureAwait(false);
        var images = await SnapshotImagesAsync(clusterStatus, run.KubeContext, greenNamespace, run.Id, logger, ct).ConfigureAwait(false);

        // Bootstrap: the first deploy immediately becomes the active slot (nothing to cut over from).
        if (string.IsNullOrWhiteSpace(activeSlot))
        {
            app?.SetActiveSlot(greenSlot, now);
            run.Succeed(log, images, now);
            return;
        }

        if (!healthy)
        {
            await namespaces.DeleteNamespaceAsync(run.KubeContext, greenNamespace, ct).ConfigureAwait(false);
            run.Fail($"green namespace '{greenNamespace}' did not become healthy — rolled back.", log, now);
            return;
        }

        if (app is not null && app.PromotionMode == Domain.Mappings.PromotionMode.Automatic)
        {
            await namespaces.DeleteNamespaceAsync(run.KubeContext, $"{run.Namespace}-{activeSlot}", ct).ConfigureAwait(false);
            app.SetActiveSlot(greenSlot, now);
            run.Succeed(log, images, now);
            return;
        }

        // Manual promotion → park; the promote/rollback handler finishes the cutover.
        run.AwaitPromotion(log, images, now);
    }

    /// <summary>Polls the namespace's overall health until Healthy or the deadline (a fresh deploy's pods take
    /// a moment to become Ready). Down/Degraded keep waiting; unreachable/timeout is unhealthy.</summary>
    private async Task<bool> HealthGateAsync(IAspireClusterStatusReader clusterStatus, string context, string ns, CancellationToken ct)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(120);
        while (DateTimeOffset.UtcNow < deadline)
        {
            try
            {
                var cluster = await clusterStatus.GetAsync(context, ns, ct).ConfigureAwait(false);
                if (cluster.Reachable && cluster.OverallHealth == Contracts.AspireApps.WorkloadHealthDto.Healthy && cluster.Workloads.Count > 0)
                    return true;
            }
            catch { /* transient — keep polling */ }
            await Task.Delay(TimeSpan.FromSeconds(5), ct).ConfigureAwait(false);
        }
        return false;
    }

    /// <summary>Reads back the just-applied workloads' images (the drift baseline). Best-effort.</summary>
    private static async Task<IReadOnlyList<DeployedImage>> SnapshotImagesAsync(
        IAspireClusterStatusReader clusterStatus, string context, string ns, Guid runId, ILogger logger, CancellationToken ct)
    {
        try
        {
            var cluster = await clusterStatus.GetAsync(context, ns, ct).ConfigureAwait(false);
            if (!cluster.Reachable) return Array.Empty<DeployedImage>();
            return cluster.Workloads
                .Where(w => !string.IsNullOrWhiteSpace(w.Image))
                .Select(w => new DeployedImage(w.Name, w.Image!))
                .ToArray();
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "[aspire] Run {Run} deployed OK but image snapshot failed.", runId);
            return Array.Empty<DeployedImage>();
        }
    }
}
