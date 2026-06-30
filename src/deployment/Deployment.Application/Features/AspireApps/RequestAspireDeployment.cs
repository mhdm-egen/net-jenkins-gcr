using Microsoft.Extensions.Logging;
using Deployment.Domain.Abstractions;
using Deployment.Domain.AspireApps;
using Deployment.Domain.AspireApps.Runs;

namespace Deployment.Application.Features.AspireApps;

/// <summary>
/// Creates an <see cref="AspireApplicationRun"/> (Pending) for a registered Aspire application. The
/// run's <c>AspireApplicationRunRequested</c> domain event drives <see cref="AspireApplicationRunExecutor"/>,
/// which shells out to aspirate off the request (bus retry + outbox).
/// </summary>
public sealed record RequestAspireDeploymentCommand(Guid ApplicationId, string? TriggeredBy);

public sealed record RequestAspireDeploymentResult(Guid? RunId, string Outcome);

public sealed class RequestAspireDeploymentHandler
{
    private readonly IAspireApplicationRepository _apps;
    private readonly IAspireApplicationRunRepository _runs;
    private readonly IUnitOfWork _uow;
    private readonly TimeProvider _clock;
    private readonly ILogger<RequestAspireDeploymentHandler> _logger;

    public RequestAspireDeploymentHandler(
        IAspireApplicationRepository apps, IAspireApplicationRunRepository runs,
        IUnitOfWork uow, TimeProvider clock, ILogger<RequestAspireDeploymentHandler> logger)
    {
        _apps = apps;
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

        var run = new AspireApplicationRun(
            id: Guid.NewGuid(),
            applicationId: app.Id,
            applicationName: app.Name,
            appHostPath: app.AppHostPath,
            kubeContext: app.KubeContext,
            @namespace: app.Namespace,
            triggeredBy: cmd.TriggeredBy ?? "manual",
            requestedAtUtc: _clock.GetUtcNow());

        await _runs.AddAsync(run, ct).ConfigureAwait(false);
        await _uow.SaveChangesAsync(ct).ConfigureAwait(false);

        _logger.LogInformation("[aspire] Requested deployment of {App} -> {Context}/{Namespace} (run {Run}).",
            app.Name, app.KubeContext, app.Namespace, run.Id);
        return new RequestAspireDeploymentResult(run.Id, "requested");
    }
}
