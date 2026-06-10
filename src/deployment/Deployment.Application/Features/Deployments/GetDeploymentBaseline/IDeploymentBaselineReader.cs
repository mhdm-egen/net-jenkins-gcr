namespace Deployment.Application.Features.Deployments.GetDeploymentBaseline;

/// <summary>
/// Read-model port for Q2 (full deployment baseline). Backed by an EF/SQL
/// projection in Infrastructure — joins span Deployment, Release,
/// DeploymentTarget, ConfigurationSetting, and DeploymentSecretBinding.
/// </summary>
public interface IDeploymentBaselineReader
{
    Task<DeploymentBaseline?> ReadAsync(Guid deploymentId, CancellationToken cancellationToken = default);
}
