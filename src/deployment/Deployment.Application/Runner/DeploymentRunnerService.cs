using Deployment.Application.Abstractions;
using Deployment.Application.Features.Deployments.BeginDeployment;
using Deployment.Application.Features.Deployments.FailDeployment;
using Deployment.Application.Features.Deployments.RecordDeploymentAudit;
using Deployment.Application.Features.Deployments.SucceedDeployment;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Deployment.Application.Runner;

/// <summary>
/// In-process deployment runner. Polls the queue, picks one Queued leaf at
/// a time, calls Begin → adapter.Execute → Succeed/Fail, recording audit
/// rows along the way. Cascade roll-up is handled by Succeed/Fail handlers
/// (see <see cref="Features.Deployments.CascadeRollup"/>).
///
/// One-at-a-time on purpose: real adapters might serialize on shared
/// resources (e.g., a slot swap on App Service). When parallelism is needed,
/// run multiple runner instances against the same DB — the
/// <see cref="IDeploymentRunnerReader.FindNextQueuedLeafAsync"/> contract
/// allows for SQL-side <c>WITH (UPDLOCK, READPAST)</c> claim semantics in
/// a future enhancement.
/// </summary>
public sealed class DeploymentRunnerService : BackgroundService
{
    private readonly IServiceProvider _services;
    private readonly IOptionsMonitor<DeploymentRunnerOptions> _options;
    private readonly ILogger<DeploymentRunnerService> _logger;

    public DeploymentRunnerService(
        IServiceProvider services,
        IOptionsMonitor<DeploymentRunnerOptions> options,
        ILogger<DeploymentRunnerService> logger)
    {
        _services = services;
        _options = options;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Deployment runner started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            bool didWork = false;
            try
            {
                didWork = await TryRunNextAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                // Don't crash the loop — log and back off.
                _logger.LogError(ex, "Deployment runner loop iteration failed.");
            }

            if (!didWork)
            {
                var delay = TimeSpan.FromSeconds(Math.Max(1, _options.CurrentValue.PollIntervalSeconds));
                try { await Task.Delay(delay, stoppingToken).ConfigureAwait(false); }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            }
        }

        _logger.LogInformation("Deployment runner stopping.");
    }

    private async Task<bool> TryRunNextAsync(CancellationToken stoppingToken)
    {
        // Each iteration owns a fresh scope so the DbContext and UoW are
        // scoped to one deployment run (no cross-row state leakage).
        using var scope = _services.CreateScope();
        var sp = scope.ServiceProvider;

        var reader = sp.GetRequiredService<IDeploymentRunnerReader>();
        var nextId = await reader.FindNextQueuedLeafAsync(stoppingToken).ConfigureAwait(false);
        if (nextId is null) return false;

        var deploymentId = nextId.Value;
        _logger.LogInformation("Picked up deployment {DeploymentId}.", deploymentId);

        var context = await reader.GetExecutionContextAsync(deploymentId, stoppingToken).ConfigureAwait(false);
        if (context is null)
        {
            // Row vanished between the find and the snapshot — fail it loudly.
            await FailAsync(sp, deploymentId, "Execution context disappeared mid-pickup.", stoppingToken).ConfigureAwait(false);
            return true;
        }

        // Resolve the adapter early so failures here don't leave the row Queued forever.
        var registry = sp.GetRequiredService<IDeploymentAdapterRegistry>();
        if (!registry.TryResolve(context.Target.TargetKind, out var adapter) || adapter is null)
        {
            await FailAsync(sp, deploymentId,
                $"No deployment adapter registered for TargetKind={context.Target.TargetKind}.",
                stoppingToken).ConfigureAwait(false);
            return true;
        }

        // Begin (Queued → Running). Persisted in its own UoW so concurrent
        // readers see the transition immediately.
        await sp.GetRequiredService<BeginDeploymentHandler>()
            .HandleAsync(new BeginDeploymentCommand(deploymentId), stoppingToken)
            .ConfigureAwait(false);

        // Adapter execution with the configured ceiling.
        var timeout = TimeSpan.FromSeconds(Math.Max(1, _options.CurrentValue.AdapterTimeoutSeconds));
        using var adapterCts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
        adapterCts.CancelAfter(timeout);

        DeploymentExecutionOutcome outcome;
        try
        {
            outcome = await adapter.ExecuteAsync(context, adapterCts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!stoppingToken.IsCancellationRequested)
        {
            outcome = DeploymentExecutionOutcome.Failure(
                $"Adapter '{adapter.GetType().Name}' exceeded {timeout.TotalSeconds:0}s.");
        }
        catch (Exception ex)
        {
            outcome = DeploymentExecutionOutcome.Failure(
                $"Adapter '{adapter.GetType().Name}' threw: {ex.GetType().Name}: {ex.Message}");
        }

        // Each transition handler runs in its own scope/UoW so partial
        // failures (e.g. SaveChanges concurrency) don't poison the runner.
        using (var terminalScope = _services.CreateScope())
        {
            var tp = terminalScope.ServiceProvider;
            if (outcome.IsSuccess)
            {
                await tp.GetRequiredService<SucceedDeploymentHandler>()
                    .HandleAsync(new SucceedDeploymentCommand(deploymentId), stoppingToken)
                    .ConfigureAwait(false);
                _logger.LogInformation("Deployment {DeploymentId} succeeded.", deploymentId);
            }
            else
            {
                await tp.GetRequiredService<FailDeploymentHandler>()
                    .HandleAsync(new FailDeploymentCommand(deploymentId, outcome.FailureReason ?? "Unknown failure."), stoppingToken)
                    .ConfigureAwait(false);
                _logger.LogWarning("Deployment {DeploymentId} failed: {Reason}", deploymentId, outcome.FailureReason);
            }
        }
        return true;
    }

    private static async Task FailAsync(IServiceProvider sp, Guid deploymentId, string reason, CancellationToken ct)
    {
        // Used for runner-side failures (no adapter, missing context). The
        // domain only allows Fail from Running, so we Begin first.
        try
        {
            await sp.GetRequiredService<BeginDeploymentHandler>()
                .HandleAsync(new BeginDeploymentCommand(deploymentId), ct).ConfigureAwait(false);
        }
        catch
        {
            // If Begin fails (e.g. row already terminal), there's nothing else to do.
            return;
        }

        await sp.GetRequiredService<FailDeploymentHandler>()
            .HandleAsync(new FailDeploymentCommand(deploymentId, reason), ct).ConfigureAwait(false);
    }
}
