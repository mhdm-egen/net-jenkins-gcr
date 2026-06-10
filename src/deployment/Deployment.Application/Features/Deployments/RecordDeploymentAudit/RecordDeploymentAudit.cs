using Deployment.Domain.Abstractions;
using Deployment.Domain.Deployments;
using FluentValidation;

namespace Deployment.Application.Features.Deployments.RecordDeploymentAudit;

/// <summary>
/// Append a free-form audit row to a deployment. The runner / adapters use
/// this for events like <c>SmokeTestPassed</c>, <c>SmokeTestFailed</c>,
/// <c>CanaryObservationStarted</c>, etc.
/// <see cref="Detail"/> is free-form (JSON by convention).
/// </summary>
public sealed record RecordDeploymentAuditCommand(
    Guid DeploymentId,
    string EventType,
    string? Detail);

public sealed class RecordDeploymentAuditValidator : AbstractValidator<RecordDeploymentAuditCommand>
{
    public RecordDeploymentAuditValidator()
    {
        RuleFor(x => x.DeploymentId).NotEmpty();
        RuleFor(x => x.EventType).NotEmpty().MaximumLength(100);
    }
}

public sealed class RecordDeploymentAuditHandler
{
    private readonly IDeploymentRepository _deployments;
    private readonly IUnitOfWork _uow;
    private readonly TimeProvider _clock;

    public RecordDeploymentAuditHandler(
        IDeploymentRepository deployments, IUnitOfWork uow, TimeProvider clock)
    {
        _deployments = deployments;
        _uow = uow;
        _clock = clock;
    }

    public async Task HandleAsync(RecordDeploymentAuditCommand cmd, CancellationToken cancellationToken = default)
    {
        var deployment = await _deployments.GetByIdAsync(cmd.DeploymentId, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Deployment {cmd.DeploymentId} not found.");

        deployment.RecordAuditEvent(Guid.NewGuid(), cmd.EventType, cmd.Detail, _clock.GetUtcNow());
        await _uow.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
