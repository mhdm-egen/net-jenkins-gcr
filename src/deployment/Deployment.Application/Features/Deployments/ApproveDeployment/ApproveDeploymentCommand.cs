using Deployment.Contracts.Deployments;

namespace Deployment.Application.Features.Deployments.ApproveDeployment;

/// <summary>
/// Decide an approval slot on a <c>Deployment</c>. If no approval row exists
/// for <paramref name="ApproverPrincipal"/> yet, one is opened first (so this
/// command works equally well as "approve" and "request-then-approve").
///
/// 4-eyes invariant (decisions §8.2):
/// <paramref name="ApproverPrincipal"/> must differ from
/// <c>Deployment.TriggeredByPrincipal</c>. Rejected by the handler when violated.
/// </summary>
public sealed record ApproveDeploymentCommand(
    Guid DeploymentId,
    Guid ApprovalId,
    string ApproverPrincipal,
    ApprovalStatusDto Verdict,
    string? Comment);
