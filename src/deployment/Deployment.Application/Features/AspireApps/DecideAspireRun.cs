using Microsoft.Extensions.Logging;
using Deployment.Application.Abstractions;
using Deployment.Domain.Abstractions;
using Deployment.Domain.AspireApps;
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

public sealed record PromoteAspireRunCommand(Guid RunId, string? PromotedBy);
public sealed record RollbackAspireRunCommand(Guid RunId, string? RolledBackBy, string? Reason);

/// <summary>Promote a blue-green Aspire run parked in <see cref="DeploymentRunStatus.AwaitingPromotion"/>:
/// make the green namespace the app's active slot and delete the old namespace.</summary>
public sealed class PromoteAspireRunHandler
{
    private readonly IAspireApplicationRunRepository _runs;
    private readonly IAspireApplicationRepository _apps;
    private readonly INamespaceManager _namespaces;
    private readonly IUnitOfWork _uow;
    private readonly TimeProvider _clock;
    private readonly ILogger<PromoteAspireRunHandler> _logger;

    public PromoteAspireRunHandler(IAspireApplicationRunRepository runs, IAspireApplicationRepository apps, INamespaceManager namespaces, IUnitOfWork uow, TimeProvider clock, ILogger<PromoteAspireRunHandler> logger)
    { _runs = runs; _apps = apps; _namespaces = namespaces; _uow = uow; _clock = clock; _logger = logger; }

    public async Task<AspireRunDecisionResult> HandleAsync(PromoteAspireRunCommand cmd, CancellationToken ct = default)
    {
        var run = await _runs.GetByIdAsync(cmd.RunId, ct).ConfigureAwait(false);
        if (run is null) return new AspireRunDecisionResult(false, "run-not-found");
        if (run.Status != DeploymentRunStatus.AwaitingPromotion) return new AspireRunDecisionResult(false, "run-not-awaiting-promotion");
        if (string.IsNullOrWhiteSpace(run.RolloutGreenSlot)) return new AspireRunDecisionResult(false, "run-missing-rollout-context");

        var now = _clock.GetUtcNow();
        if (!string.IsNullOrWhiteSpace(run.RolloutActiveSlot))
            await _namespaces.DeleteNamespaceAsync(run.KubeContext, $"{run.Namespace}-{run.RolloutActiveSlot}", ct).ConfigureAwait(false);

        var app = await _apps.GetByIdAsync(run.ApplicationId, ct).ConfigureAwait(false);
        app?.SetActiveSlot(run.RolloutGreenSlot!, now);
        run.PromoteRollout(cmd.PromotedBy ?? "unknown", now);
        await _uow.SaveChangesAsync(ct).ConfigureAwait(false);
        _logger.LogInformation("[aspire] Run {Run} promoted to slot '{Slot}' by {By}.", run.Id, run.RolloutGreenSlot, run.DecisionBy);
        return new AspireRunDecisionResult(true, "promoted");
    }
}

/// <summary>Roll a blue-green Aspire run back: delete the green namespace; the active deploy stays live.</summary>
public sealed class RollbackAspireRunHandler
{
    private readonly IAspireApplicationRunRepository _runs;
    private readonly INamespaceManager _namespaces;
    private readonly IUnitOfWork _uow;
    private readonly TimeProvider _clock;
    private readonly ILogger<RollbackAspireRunHandler> _logger;

    public RollbackAspireRunHandler(IAspireApplicationRunRepository runs, INamespaceManager namespaces, IUnitOfWork uow, TimeProvider clock, ILogger<RollbackAspireRunHandler> logger)
    { _runs = runs; _namespaces = namespaces; _uow = uow; _clock = clock; _logger = logger; }

    public async Task<AspireRunDecisionResult> HandleAsync(RollbackAspireRunCommand cmd, CancellationToken ct = default)
    {
        var run = await _runs.GetByIdAsync(cmd.RunId, ct).ConfigureAwait(false);
        if (run is null) return new AspireRunDecisionResult(false, "run-not-found");
        if (run.Status != DeploymentRunStatus.AwaitingPromotion) return new AspireRunDecisionResult(false, "run-not-awaiting-promotion");
        if (string.IsNullOrWhiteSpace(run.RolloutGreenSlot)) return new AspireRunDecisionResult(false, "run-missing-rollout-context");

        await _namespaces.DeleteNamespaceAsync(run.KubeContext, $"{run.Namespace}-{run.RolloutGreenSlot}", ct).ConfigureAwait(false);
        run.RollbackRollout(cmd.RolledBackBy ?? "unknown", cmd.Reason, _clock.GetUtcNow());
        await _uow.SaveChangesAsync(ct).ConfigureAwait(false);
        _logger.LogInformation("[aspire] Run {Run} rolled back (green '{Slot}' deleted) by {By}.", run.Id, run.RolloutGreenSlot, run.DecisionBy);
        return new AspireRunDecisionResult(true, "rolled-back");
    }
}
