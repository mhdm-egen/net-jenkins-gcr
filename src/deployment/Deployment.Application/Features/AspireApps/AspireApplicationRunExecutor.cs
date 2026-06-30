using Microsoft.Extensions.Logging;
using Deployment.Application.Abstractions;
using Deployment.Domain.Abstractions;
using Deployment.Domain.AspireApps.Runs;
using Deployment.Domain.AspireApps.Runs.Events;
using Deployment.Domain.Runs;
using Wolverine.Attributes;

namespace Deployment.Application.Features.AspireApps;

/// <summary>
/// Executes a requested Aspire-application deployment by shelling out to Aspir8 via
/// <see cref="IAspirateRunner"/>, then settles the run. Discovered by Wolverine as a handler for
/// <see cref="AspireApplicationRunRequested"/>, so it runs asynchronously off the request with the
/// bus's retry + SQL outbox. Mirrors <c>DeploymentRunExecutor</c>.
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
        IAspirateRunner aspirate,
        IUnitOfWork uow,
        TimeProvider clock,
        ILogger<AspireApplicationRunExecutor> logger,
        CancellationToken ct)
    {
        var run = await runs.GetByIdAsync(evt.RunId, ct).ConfigureAwait(false);
        if (run is null || run.Status != DeploymentRunStatus.Pending) return; // idempotent under retries

        run.Start();

        AspirateDeployResult result;
        try
        {
            result = await aspirate.DeployAsync(
                new AspirateDeployRequest(run.ManifestSource, run.KubeContext, run.Namespace), ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[aspire] Run {Run} threw.", run.Id);
            result = new AspirateDeployResult(false, ex.ToString(), ex.Message);
        }

        var now = clock.GetUtcNow();
        if (result.Success) run.Succeed(result.Log, now);
        else run.Fail(result.FailureReason ?? "aspirate deploy failed.", result.Log, now);

        await uow.SaveChangesAsync(ct).ConfigureAwait(false);
        logger.LogInformation("[aspire] Run {Run} -> {Status}.", run.Id, run.Status);
    }
}
