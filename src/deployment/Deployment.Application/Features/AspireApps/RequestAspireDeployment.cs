using Microsoft.Extensions.Logging;
using Deployment.Domain.Abstractions;
using Deployment.Domain.AspireApps;
using Deployment.Domain.AspireApps.Runs;
using Deployment.Domain.Environments;
using Deployment.Domain.Mappings;

namespace Deployment.Application.Features.AspireApps;

/// <summary>
/// Creates an <see cref="AspireApplicationRun"/> (Pending) for a registered Aspire application: resolves
/// the target environment, snapshots its kube context/namespace + the app's manifest source/version onto
/// the run, and saves it. The run's <c>AspireApplicationRunRequested</c> domain event drives
/// <see cref="AspireApplicationRunExecutor"/>, which shells out to aspirate off the request.
/// </summary>
public sealed record RequestAspireDeploymentCommand(Guid ApplicationId, string? TriggeredBy);

public sealed record RequestAspireDeploymentResult(Guid? RunId, string Outcome);

public sealed class RequestAspireDeploymentHandler
{
    private readonly IAspireApplicationRepository _apps;
    private readonly IEnvironmentRepository _envs;
    private readonly IAspireApplicationRunRepository _runs;
    private readonly IUnitOfWork _uow;
    private readonly TimeProvider _clock;
    private readonly ILogger<RequestAspireDeploymentHandler> _logger;

    public RequestAspireDeploymentHandler(
        IAspireApplicationRepository apps, IEnvironmentRepository envs, IAspireApplicationRunRepository runs,
        IUnitOfWork uow, TimeProvider clock, ILogger<RequestAspireDeploymentHandler> logger)
    {
        _apps = apps;
        _envs = envs;
        _runs = runs;
        _uow = uow;
        _clock = clock;
        _logger = logger;
    }

    public async Task<RequestAspireDeploymentResult> HandleAsync(RequestAspireDeploymentCommand cmd, CancellationToken ct = default)
    {
        var app = await _apps.GetByIdAsync(cmd.ApplicationId, ct).ConfigureAwait(false);
        if (app is null) return new RequestAspireDeploymentResult(null, "application-not-found");
        if (!app.IsActive) return new RequestAspireDeploymentResult(null, "application-inactive");

        var env = await _envs.GetByIdAsync(app.EnvironmentId, ct).ConfigureAwait(false);
        if (env is null) return new RequestAspireDeploymentResult(null, "environment-not-found");
        if (string.IsNullOrWhiteSpace(env.KubernetesContext) || string.IsNullOrWhiteSpace(env.KubernetesNamespace))
            return new RequestAspireDeploymentResult(null, "environment-not-kubernetes");

        // Blue-green: deploy to the inactive slot's namespace ({base}-{green}); the executor health-gates it
        // and (auto or manual) promotes by making it the app's active slot + retiring the old namespace.
        string? greenSlot = null, activeSlot = null;
        if (app.Strategy == RolloutStrategy.BlueGreen)
        {
            activeSlot = app.ActiveSlot; // null on the first (bootstrap) deploy
            greenSlot = string.Equals(activeSlot, "blue", StringComparison.OrdinalIgnoreCase) ? "green" : "blue";
        }

        var run = new AspireApplicationRun(
            id: Guid.NewGuid(),
            applicationId: app.Id,
            applicationName: app.Name,
            environmentId: env.Id,
            environmentName: env.Name,
            kubeContext: env.KubernetesContext!,
            @namespace: env.KubernetesNamespace!,
            manifestSource: app.ManifestSource,
            version: app.Version,
            triggeredBy: cmd.TriggeredBy ?? "manual",
            requestedAtUtc: _clock.GetUtcNow(),
            requiresApproval: env.IsProtected,
            rolloutGreenSlot: greenSlot,
            rolloutActiveSlot: activeSlot);

        await _runs.AddAsync(run, ct).ConfigureAwait(false);
        await _uow.SaveChangesAsync(ct).ConfigureAwait(false);

        _logger.LogInformation("[aspire] Requested deployment of {App} -> {Env} ({Context}/{Namespace}) (run {Run}).",
            app.Name, env.Name, env.KubernetesContext, env.KubernetesNamespace, run.Id);
        return new RequestAspireDeploymentResult(run.Id, "requested");
    }
}
