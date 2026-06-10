using Deployment.Domain.Common;
using Deployment.Domain.Deployments.Events;

namespace Deployment.Domain.Deployments;

/// <summary>
/// A single row representing the act of installing a <c>Release</c> into an
/// <c>Environment</c>, optionally targeting a specific <c>DeploymentTarget</c>.
///
/// Cascade convention (decisions §10.2): a row with <see cref="TargetId"/>
/// <c>null</c> is always a logical parent — the application-level fan-out
/// header. Concrete deploys always live at the leaves with non-null
/// <see cref="TargetId"/> and reference the parent via
/// <see cref="ParentDeploymentId"/>.
///
/// Each cascade row is its own aggregate. The application-layer
/// <c>StartDeployment</c> handler maintains the parent ↔ children
/// relationship across aggregates within a single Unit of Work.
///
/// State machine: see <see cref="DeploymentStatus"/>. The only transition out of
/// <see cref="DeploymentStatus.Succeeded"/> is <see cref="DeploymentStatus.RolledBack"/>,
/// applied when a *new* rollback deployment for the prior release succeeds —
/// the original row preserves "every row is immutable history except for Status."
/// </summary>
public sealed class Deployment : AggregateRoot<Guid>
{
    public Guid ReleaseId { get; private set; }
    public Guid EnvironmentId { get; private set; }
    public Guid? TargetId { get; private set; }
    public Guid? ParentDeploymentId { get; private set; }

    public DeploymentStatus Status { get; private set; }
    public DeploymentStrategy Strategy { get; private set; }
    public DeploymentTrigger Trigger { get; private set; }
    public string TriggeredByPrincipal { get; private set; }

    public DateTimeOffset? StartedAtUtc { get; private set; }
    public DateTimeOffset? CompletedAtUtc { get; private set; }

    public string? SkipPromotionPathReason { get; private set; }
    public string? OverrideFreezeReason { get; private set; }
    public string? FailureReason { get; private set; }
    public string? CancellationReason { get; private set; }

    /// <summary>Set when this row is the original of a rollback pair.</summary>
    public Guid? RolledBackByDeploymentId { get; private set; }

    private readonly List<Approval> _approvals = new();
    public IReadOnlyCollection<Approval> Approvals => _approvals.AsReadOnly();

    private readonly List<DeploymentEvent> _events = new();
    public IReadOnlyCollection<DeploymentEvent> Events => _events.AsReadOnly();

    private readonly List<DeploymentSecretBinding> _secretBindings = new();
    public IReadOnlyCollection<DeploymentSecretBinding> SecretBindings => _secretBindings.AsReadOnly();

    public bool IsCascadeParent => TargetId is null;
    public bool IsTerminal =>
        Status is DeploymentStatus.Succeeded
              or DeploymentStatus.Failed
              or DeploymentStatus.RolledBack
              or DeploymentStatus.Cancelled;

    private Deployment()
    {
        TriggeredByPrincipal = string.Empty;
    }

    public Deployment(
        Guid id,
        Guid releaseId,
        Guid environmentId,
        Guid? targetId,
        Guid? parentDeploymentId,
        DeploymentStrategy strategy,
        DeploymentTrigger trigger,
        string triggeredByPrincipal,
        string? skipPromotionPathReason,
        string? overrideFreezeReason,
        DateTimeOffset queuedAtUtc)
    {
        if (id == Guid.Empty) throw new ArgumentException("Id cannot be empty.", nameof(id));
        if (releaseId == Guid.Empty)
            throw new ArgumentException("ReleaseId cannot be empty.", nameof(releaseId));
        if (environmentId == Guid.Empty)
            throw new ArgumentException("EnvironmentId cannot be empty.", nameof(environmentId));
        if (string.IsNullOrWhiteSpace(triggeredByPrincipal))
            throw new ArgumentException("TriggeredByPrincipal cannot be empty.", nameof(triggeredByPrincipal));

        Id = id;
        ReleaseId = releaseId;
        EnvironmentId = environmentId;
        TargetId = targetId;
        ParentDeploymentId = parentDeploymentId;
        Status = DeploymentStatus.Queued;
        Strategy = strategy;
        Trigger = trigger;
        TriggeredByPrincipal = triggeredByPrincipal.Trim();
        SkipPromotionPathReason = string.IsNullOrWhiteSpace(skipPromotionPathReason) ? null : skipPromotionPathReason.Trim();
        OverrideFreezeReason = string.IsNullOrWhiteSpace(overrideFreezeReason) ? null : overrideFreezeReason.Trim();

        RaiseEvent(new DeploymentQueued(
            id, releaseId, environmentId, targetId, parentDeploymentId,
            strategy, trigger, TriggeredByPrincipal,
            SkipPromotionPathReason, OverrideFreezeReason, queuedAtUtc));
    }

    // --- State transitions ---

    public void Start(DateTimeOffset startedAtUtc)
    {
        if (Status != DeploymentStatus.Queued)
            throw new InvalidOperationException(
                $"Cannot start deployment {Id}: status is {Status}, expected Queued.");

        Status = DeploymentStatus.Running;
        StartedAtUtc = startedAtUtc;
        RaiseEvent(new DeploymentStarted(Id, startedAtUtc, startedAtUtc));
    }

    public void Succeed(DateTimeOffset completedAtUtc)
    {
        if (Status != DeploymentStatus.Running)
            throw new InvalidOperationException(
                $"Cannot succeed deployment {Id}: status is {Status}, expected Running.");

        Status = DeploymentStatus.Succeeded;
        CompletedAtUtc = completedAtUtc;
        RaiseEvent(new DeploymentSucceeded(Id, completedAtUtc, completedAtUtc));
    }

    public void Fail(string failureReason, DateTimeOffset completedAtUtc)
    {
        if (Status != DeploymentStatus.Running)
            throw new InvalidOperationException(
                $"Cannot fail deployment {Id}: status is {Status}, expected Running.");
        if (string.IsNullOrWhiteSpace(failureReason))
            throw new ArgumentException("FailureReason cannot be empty.", nameof(failureReason));

        Status = DeploymentStatus.Failed;
        CompletedAtUtc = completedAtUtc;
        FailureReason = failureReason.Trim();
        RaiseEvent(new DeploymentFailed(Id, FailureReason, completedAtUtc, completedAtUtc));
    }

    /// <summary>
    /// Cancel a queued deployment (operator action or queue cleanup; not allowed
    /// from Running in v1 — decisions §6.3).
    /// </summary>
    public void Cancel(string cancellationReason, DateTimeOffset cancelledAtUtc)
    {
        if (Status != DeploymentStatus.Queued)
            throw new InvalidOperationException(
                $"Cannot cancel deployment {Id}: status is {Status}, expected Queued.");
        if (string.IsNullOrWhiteSpace(cancellationReason))
            throw new ArgumentException("CancellationReason cannot be empty.", nameof(cancellationReason));

        Status = DeploymentStatus.Cancelled;
        CompletedAtUtc = cancelledAtUtc;
        CancellationReason = cancellationReason.Trim();
        RaiseEvent(new DeploymentCancelled(Id, CancellationReason, cancelledAtUtc));
    }

    /// <summary>
    /// Flip a previously-<see cref="DeploymentStatus.Succeeded"/> row to
    /// <see cref="DeploymentStatus.RolledBack"/> when a *new* rollback
    /// deployment (a separate aggregate) succeeded. The
    /// <paramref name="rollbackDeploymentId"/> is the new row that
    /// performed the rollback — captured here for traceability.
    /// </summary>
    public void MarkRolledBack(Guid rollbackDeploymentId, DateTimeOffset occurredAtUtc)
    {
        if (Status != DeploymentStatus.Succeeded)
            throw new InvalidOperationException(
                $"Cannot mark deployment {Id} as RolledBack: status is {Status}, expected Succeeded.");
        if (rollbackDeploymentId == Guid.Empty)
            throw new ArgumentException("RollbackDeploymentId cannot be empty.", nameof(rollbackDeploymentId));

        Status = DeploymentStatus.RolledBack;
        RolledBackByDeploymentId = rollbackDeploymentId;
        RaiseEvent(new DeploymentRolledBack(Id, rollbackDeploymentId, occurredAtUtc));
    }

    // --- Approvals ---

    /// <summary>
    /// Open an approval slot for <paramref name="approverPrincipal"/>. Multiple
    /// approvals can be opened per deployment (e.g., N-eyes). The 4-eyes
    /// invariant (approver ≠ triggerer) is enforced at decision time by the
    /// application handler, not here.
    /// </summary>
    public Approval RequestApproval(Guid approvalId, string approverPrincipal, DateTimeOffset occurredAtUtc)
    {
        if (_approvals.Any(a => a.Id == approvalId))
            throw new InvalidOperationException($"Approval {approvalId} already exists on deployment {Id}.");

        var approval = new Approval(approvalId, Id, approverPrincipal);
        _approvals.Add(approval);
        RaiseEvent(new ApprovalRequested(Id, approval.Id, approval.ApproverPrincipal, occurredAtUtc));
        return approval;
    }

    public void DecideApproval(
        Guid approvalId,
        ApprovalStatus verdict,
        string? comment,
        DateTimeOffset decidedAtUtc)
    {
        var approval = _approvals.FirstOrDefault(a => a.Id == approvalId)
            ?? throw new InvalidOperationException(
                $"Approval {approvalId} not found on deployment {Id}.");

        approval.Decide(verdict, comment, decidedAtUtc);
        RaiseEvent(new ApprovalDecided(Id, approval.Id, approval.ApproverPrincipal, verdict, approval.Comment, decidedAtUtc));
    }

    // --- Audit events ---

    /// <summary>
    /// Append an audit row. Examples: <c>SmokeTestRun</c>, <c>SmokeTestPassed</c>,
    /// <c>SmokeTestFailed</c>, <c>CurrentPinFallbackApplied</c>. <paramref name="detail"/>
    /// is free-form (JSON by convention).
    /// </summary>
    public DeploymentEvent RecordAuditEvent(
        Guid eventId,
        string eventType,
        string? detail,
        DateTimeOffset timestamp)
    {
        var evt = new DeploymentEvent(eventId, Id, timestamp, eventType, detail);
        _events.Add(evt);
        RaiseEvent(new DeploymentAuditEventRecorded(Id, evt.Id, evt.EventType, evt.Detail, timestamp));
        return evt;
    }

    // --- Secret bindings ---

    public DeploymentSecretBinding AddSecretBinding(
        Guid configurationSettingId,
        string resolvedSecretUri,
        DateTimeOffset resolvedAtUtc)
    {
        if (_secretBindings.Any(b => b.ConfigurationSettingId == configurationSettingId))
            throw new InvalidOperationException(
                $"Secret binding for configuration {configurationSettingId} already exists on deployment {Id}.");

        var binding = new DeploymentSecretBinding(Id, configurationSettingId, resolvedSecretUri, resolvedAtUtc);
        _secretBindings.Add(binding);
        RaiseEvent(new DeploymentSecretBindingResolved(Id, configurationSettingId, binding.ResolvedSecretUri, resolvedAtUtc));
        return binding;
    }
}
