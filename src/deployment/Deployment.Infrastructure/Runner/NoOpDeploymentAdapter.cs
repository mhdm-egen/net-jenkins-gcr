using Deployment.Application.Abstractions;
using Deployment.Contracts.Environments;
using Microsoft.Extensions.Logging;

namespace Deployment.Infrastructure.Runner;

/// <summary>
/// Catch-all adapter used as a fallback when no real driver is registered
/// for a given <see cref="TargetKindDto"/>. Sleeps briefly to simulate work,
/// logs what it would have done, and reports success. Useful for end-to-end
/// UI demos and for scaffolding a runner before any real cloud adapter ships.
/// </summary>
internal sealed class NoOpDeploymentAdapter : IDeploymentAdapter
{
    private readonly ILogger<NoOpDeploymentAdapter> _logger;

    public NoOpDeploymentAdapter(ILogger<NoOpDeploymentAdapter> logger)
    {
        _logger = logger;
    }

    // Registered as a fallback — the registry tries kind-specific adapters
    // first and falls back to this one. The property value is arbitrary;
    // resolution is by registration, not by this self-report.
    public TargetKindDto TargetKind => TargetKindDto.VM;

    public async Task<DeploymentExecutionOutcome> ExecuteAsync(
        DeploymentExecutionContext context, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "[noop adapter] Would deploy {Unit} v{Version} to {Kind} '{Resource}' in {Region}; {BindingCount} secret binding(s).",
            context.DeployableUnitName, context.ReleaseSemanticVersion,
            context.Target.TargetKind, context.Target.ResourceId, context.Target.Region,
            context.SecretBindings.Count);

        // Simulate a bit of work so the UI shows Running for a moment.
        await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken).ConfigureAwait(false);

        return DeploymentExecutionOutcome.Success;
    }
}
