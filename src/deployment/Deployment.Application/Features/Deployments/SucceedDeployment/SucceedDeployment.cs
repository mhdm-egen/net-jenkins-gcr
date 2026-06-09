using Deployment.Domain.Abstractions;
using Deployment.Domain.Deployments;
using FluentValidation;

namespace Deployment.Application.Features.Deployments.SucceedDeployment;

/// <summary>
/// Transition <c>Running → Succeeded</c>. The handler also rolls up the
/// cascade parent: if this row's siblings have all reached a terminal state
/// and *all* succeeded, the parent flips Succeeded too.
/// </summary>
public sealed record SucceedDeploymentCommand(Guid DeploymentId);

public sealed class SucceedDeploymentValidator : AbstractValidator<SucceedDeploymentCommand>
{
    public SucceedDeploymentValidator() => RuleFor(x => x.DeploymentId).NotEmpty();
}

public sealed class SucceedDeploymentHandler
{
    private readonly IDeploymentRepository _deployments;
    private readonly IUnitOfWork _uow;
    private readonly TimeProvider _clock;

    public SucceedDeploymentHandler(IDeploymentRepository deployments, IUnitOfWork uow, TimeProvider clock)
    {
        _deployments = deployments;
        _uow = uow;
        _clock = clock;
    }

    public async Task HandleAsync(SucceedDeploymentCommand cmd, CancellationToken cancellationToken = default)
    {
        var deployment = await _deployments.GetByIdAsync(cmd.DeploymentId, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Deployment {cmd.DeploymentId} not found.");

        var now = _clock.GetUtcNow();
        deployment.Succeed(now);

        // Cascade roll-up: if this child completes the parent set with all
        // children succeeded, the parent transitions Running → Succeeded too.
        // The parent's Begin happens lazily here when the first child succeeds;
        // see CascadeRollup helper for the policy.
        if (deployment.ParentDeploymentId is { } parentId)
        {
            await CascadeRollup.OnChildTerminalAsync(
                parentId, _deployments, _clock, cancellationToken).ConfigureAwait(false);
        }

        await _uow.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
