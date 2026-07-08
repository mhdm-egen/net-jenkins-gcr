using Microsoft.Extensions.Logging;
using Deployment.Domain.Abstractions;
using Deployment.Domain.AspireApps.Runs;
using Deployment.Domain.Runs;

namespace Deployment.Application.Features.AspireApps;

/// <summary>Approve or reject an Aspire run parked in <see cref="DeploymentRunStatus.AwaitingApproval"/>
/// (it targeted a protected environment). Approve transitions it to Pending and re-raises the run
/// request so the executor applies it; Reject terminates it with nothing applied.</summary>
public sealed record AspireRunDecisionResult(bool Applied, string Outcome);

public sealed record ApproveAspireRunCommand(Guid RunId, string? ApprovedBy);
public sealed record RejectAspireRunCommand(Guid RunId, string? RejectedBy, string? Reason);

public sealed class ApproveAspireRunHandler
{
    private readonly IAspireApplicationRunRepository _runs;
    private readonly IUnitOfWork _uow;
    private readonly TimeProvider _clock;
    private readonly ILogger<ApproveAspireRunHandler> _logger;

    public ApproveAspireRunHandler(IAspireApplicationRunRepository runs, IUnitOfWork uow, TimeProvider clock, ILogger<ApproveAspireRunHandler> logger)
    { _runs = runs; _uow = uow; _clock = clock; _logger = logger; }

    public async Task<AspireRunDecisionResult> HandleAsync(ApproveAspireRunCommand cmd, CancellationToken ct = default)
    {
        var run = await _runs.GetByIdAsync(cmd.RunId, ct).ConfigureAwait(false);
        if (run is null) return new AspireRunDecisionResult(false, "run-not-found");
        if (run.Status != DeploymentRunStatus.AwaitingApproval) return new AspireRunDecisionResult(false, "run-not-awaiting-approval");

        run.Approve(cmd.ApprovedBy ?? "unknown", _clock.GetUtcNow());
        await _uow.SaveChangesAsync(ct).ConfigureAwait(false); // raises AspireApplicationRunRequested → executor deploys
        _logger.LogInformation("[aspire] Run {Run} approved by {By} → deploying.", run.Id, run.DecisionBy);
        return new AspireRunDecisionResult(true, "approved");
    }
}

public sealed class RejectAspireRunHandler
{
    private readonly IAspireApplicationRunRepository _runs;
    private readonly IUnitOfWork _uow;
    private readonly TimeProvider _clock;
    private readonly ILogger<RejectAspireRunHandler> _logger;

    public RejectAspireRunHandler(IAspireApplicationRunRepository runs, IUnitOfWork uow, TimeProvider clock, ILogger<RejectAspireRunHandler> logger)
    { _runs = runs; _uow = uow; _clock = clock; _logger = logger; }

    public async Task<AspireRunDecisionResult> HandleAsync(RejectAspireRunCommand cmd, CancellationToken ct = default)
    {
        var run = await _runs.GetByIdAsync(cmd.RunId, ct).ConfigureAwait(false);
        if (run is null) return new AspireRunDecisionResult(false, "run-not-found");
        if (run.Status != DeploymentRunStatus.AwaitingApproval) return new AspireRunDecisionResult(false, "run-not-awaiting-approval");

        run.Reject(cmd.RejectedBy ?? "unknown", cmd.Reason, _clock.GetUtcNow());
        await _uow.SaveChangesAsync(ct).ConfigureAwait(false);
        _logger.LogInformation("[aspire] Run {Run} rejected by {By}.", run.Id, run.DecisionBy);
        return new AspireRunDecisionResult(true, "rejected");
    }
}
