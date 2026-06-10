using Deployment.Domain.Abstractions;
using Deployment.Domain.Deployments;
using FluentValidation;

namespace Deployment.Application.Features.Deployments.BeginDeployment;

/// <summary>
/// Transition <c>Queued → Running</c>. Called by the deployment runner when it
/// picks up a row, or by an external runner via HTTP.
/// </summary>
public sealed record BeginDeploymentCommand(Guid DeploymentId);

public sealed class BeginDeploymentValidator : AbstractValidator<BeginDeploymentCommand>
{
    public BeginDeploymentValidator() => RuleFor(x => x.DeploymentId).NotEmpty();
}

public sealed class BeginDeploymentHandler
{
    private readonly IDeploymentRepository _deployments;
    private readonly IUnitOfWork _uow;
    private readonly TimeProvider _clock;

    public BeginDeploymentHandler(IDeploymentRepository deployments, IUnitOfWork uow, TimeProvider clock)
    {
        _deployments = deployments;
        _uow = uow;
        _clock = clock;
    }

    public async Task HandleAsync(BeginDeploymentCommand cmd, CancellationToken cancellationToken = default)
    {
        var deployment = await _deployments.GetByIdAsync(cmd.DeploymentId, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Deployment {cmd.DeploymentId} not found.");

        deployment.Start(_clock.GetUtcNow());
        await _uow.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
