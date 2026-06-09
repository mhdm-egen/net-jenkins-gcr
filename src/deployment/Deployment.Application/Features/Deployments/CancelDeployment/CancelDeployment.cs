using Deployment.Domain.Abstractions;
using Deployment.Domain.Deployments;
using FluentValidation;

namespace Deployment.Application.Features.Deployments.CancelDeployment;

/// <summary>
/// Cancel a <c>Queued</c> deployment. The domain rejects any other source
/// state (decisions §6.3 — Cancel is not allowed from Running in v1).
/// </summary>
public sealed record CancelDeploymentCommand(Guid DeploymentId, string CancellationReason);

public sealed class CancelDeploymentValidator : AbstractValidator<CancelDeploymentCommand>
{
    public CancelDeploymentValidator()
    {
        RuleFor(x => x.DeploymentId).NotEmpty();
        RuleFor(x => x.CancellationReason).NotEmpty().MaximumLength(500);
    }
}

public sealed class CancelDeploymentHandler
{
    private readonly IDeploymentRepository _deployments;
    private readonly IUnitOfWork _uow;
    private readonly TimeProvider _clock;

    public CancelDeploymentHandler(IDeploymentRepository deployments, IUnitOfWork uow, TimeProvider clock)
    {
        _deployments = deployments;
        _uow = uow;
        _clock = clock;
    }

    public async Task HandleAsync(CancelDeploymentCommand cmd, CancellationToken cancellationToken = default)
    {
        var deployment = await _deployments.GetByIdAsync(cmd.DeploymentId, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Deployment {cmd.DeploymentId} not found.");

        deployment.Cancel(cmd.CancellationReason, _clock.GetUtcNow());
        await _uow.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
