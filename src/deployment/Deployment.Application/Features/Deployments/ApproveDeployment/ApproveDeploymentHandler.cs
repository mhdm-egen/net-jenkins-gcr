using Deployment.Domain.Abstractions;
using Deployment.Domain.Deployments;

namespace Deployment.Application.Features.Deployments.ApproveDeployment;

public sealed class ApproveDeploymentHandler
{
    private readonly IDeploymentRepository _deployments;
    private readonly IUnitOfWork _uow;
    private readonly TimeProvider _clock;

    public ApproveDeploymentHandler(
        IDeploymentRepository deployments,
        IUnitOfWork uow,
        TimeProvider clock)
    {
        _deployments = deployments;
        _uow = uow;
        _clock = clock;
    }

    public async Task HandleAsync(ApproveDeploymentCommand cmd, CancellationToken cancellationToken = default)
    {
        var deployment = await _deployments.GetByIdAsync(cmd.DeploymentId, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Deployment {cmd.DeploymentId} not found.");

        var principal = cmd.ApproverPrincipal.Trim();
        if (string.Equals(deployment.TriggeredByPrincipal, principal, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException(
                "Segregation-of-duties violation: the approver cannot be the same principal that triggered the deployment (4-eyes principle).");

        var now = _clock.GetUtcNow();
        var existing = deployment.Approvals.FirstOrDefault(a => a.Id == cmd.ApprovalId);
        if (existing is null)
        {
            deployment.RequestApproval(cmd.ApprovalId, principal, now);
        }
        else if (!string.Equals(existing.ApproverPrincipal, principal, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Approval {cmd.ApprovalId} belongs to a different principal ({existing.ApproverPrincipal}).");
        }

        deployment.DecideApproval(cmd.ApprovalId, cmd.Verdict.ToDomain(), cmd.Comment, now);

        await _uow.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
