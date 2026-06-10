using Deployment.Domain.Abstractions;
using Deployment.Domain.Deployments;
using FluentValidation;

namespace Deployment.Application.Features.Deployments.FailDeployment;

/// <summary>
/// Transition <c>Running → Failed</c>. The handler rolls up to the cascade
/// parent immediately per decisions §5.3 (StopAndManual semantics in v1):
/// the parent is marked Failed as soon as any child fails, even if other
/// children are still Queued/Running.
/// </summary>
public sealed record FailDeploymentCommand(Guid DeploymentId, string FailureReason);

public sealed class FailDeploymentValidator : AbstractValidator<FailDeploymentCommand>
{
    public FailDeploymentValidator()
    {
        RuleFor(x => x.DeploymentId).NotEmpty();
        RuleFor(x => x.FailureReason).NotEmpty().MaximumLength(2000);
    }
}

public sealed class FailDeploymentHandler
{
    private readonly IDeploymentRepository _deployments;
    private readonly IUnitOfWork _uow;
    private readonly TimeProvider _clock;

    public FailDeploymentHandler(IDeploymentRepository deployments, IUnitOfWork uow, TimeProvider clock)
    {
        _deployments = deployments;
        _uow = uow;
        _clock = clock;
    }

    public async Task HandleAsync(FailDeploymentCommand cmd, CancellationToken cancellationToken = default)
    {
        var deployment = await _deployments.GetByIdAsync(cmd.DeploymentId, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Deployment {cmd.DeploymentId} not found.");

        var now = _clock.GetUtcNow();
        deployment.Fail(cmd.FailureReason, now);

        if (deployment.ParentDeploymentId is { } parentId)
        {
            await CascadeRollup.OnChildFailedAsync(
                parentId,
                $"Child {deployment.Id} failed: {cmd.FailureReason}",
                _deployments, _clock, cancellationToken).ConfigureAwait(false);
        }

        await _uow.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
