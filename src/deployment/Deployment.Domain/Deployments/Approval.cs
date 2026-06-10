using Deployment.Domain.Common;

namespace Deployment.Domain.Deployments;

/// <summary>
/// Gate on a <see cref="Deployment"/>. Pending until the approver decides;
/// once decided, immutable. The 4-eyes invariant
/// (ApproverPrincipal ≠ Deployment.TriggeredByPrincipal) is enforced by the
/// <c>ApproveDeployment</c> handler — not here, since this entity doesn't
/// know who triggered the deployment.
///
/// Append-only: a rejected approval doesn't get "un-rejected"; a fresh
/// Approval row is added for a second attempt.
/// </summary>
public sealed class Approval : Entity<Guid>
{
    public Guid DeploymentId { get; private set; }
    public string ApproverPrincipal { get; private set; }
    public ApprovalStatus Status { get; private set; }
    public DateTimeOffset? DecidedAtUtc { get; private set; }
    public string? Comment { get; private set; }

    private Approval()
    {
        ApproverPrincipal = string.Empty;
    }

    internal Approval(Guid id, Guid deploymentId, string approverPrincipal)
    {
        if (id == Guid.Empty) throw new ArgumentException("Id cannot be empty.", nameof(id));
        if (deploymentId == Guid.Empty)
            throw new ArgumentException("DeploymentId cannot be empty.", nameof(deploymentId));
        if (string.IsNullOrWhiteSpace(approverPrincipal))
            throw new ArgumentException("ApproverPrincipal cannot be empty.", nameof(approverPrincipal));

        Id = id;
        DeploymentId = deploymentId;
        ApproverPrincipal = approverPrincipal.Trim();
        Status = ApprovalStatus.Pending;
    }

    internal void Decide(ApprovalStatus verdict, string? comment, DateTimeOffset decidedAtUtc)
    {
        if (Status != ApprovalStatus.Pending)
            throw new InvalidOperationException(
                $"Approval {Id} is already {Status}; cannot change to {verdict}.");
        if (verdict is not (ApprovalStatus.Approved or ApprovalStatus.Rejected))
            throw new ArgumentOutOfRangeException(nameof(verdict),
                "Decide only accepts Approved or Rejected.");

        Status = verdict;
        DecidedAtUtc = decidedAtUtc;
        Comment = string.IsNullOrWhiteSpace(comment) ? null : comment.Trim();
    }
}
