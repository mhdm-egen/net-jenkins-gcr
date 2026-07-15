namespace Deployment.Application.Abstractions;

/// <summary>Inputs for a blue-green slot deploy (one Deployment + a shared Service).</summary>
public sealed record BlueGreenDeployRequest(
    string Context, string Namespace, string Name, string Image, int ContainerPort, int Replicas,
    IReadOnlyDictionary<string, string> EnvVars, string? ImagePullSecret);

/// <summary>Result of deploying + health-gating the green slot. <see cref="GreenSlot"/> is the slot just
/// deployed; <see cref="ActiveSlot"/> is the slot the Service still routes to (equal to GreenSlot only on
/// the very first "bootstrap" deploy, where green immediately takes traffic).</summary>
public sealed record BlueGreenDeployResult(string GreenSlot, string ActiveSlot, bool Healthy, string Detail);

/// <summary>
/// Blue-green mechanics on vanilla Kubernetes: two Deployments (<c>{name}-blue</c>/<c>{name}-green</c>)
/// distinguished by a <c>slot</c> label and one Service that selects the active slot. Implemented in
/// Infrastructure over the KubernetesClient. The Service selector swap is the traffic cutover.
/// </summary>
public interface IBlueGreenDeployer
{
    /// <summary>Deploy the new version to the inactive slot and health-gate it. Does NOT switch traffic
    /// (except the first bootstrap deploy, which creates the Service pointing at the new slot).</summary>
    Task<BlueGreenDeployResult> DeployGreenAsync(BlueGreenDeployRequest request, CancellationToken cancellationToken = default);

    /// <summary>Cut traffic over to <paramref name="greenSlot"/> (patch the Service selector) and retire
    /// <paramref name="oldSlot"/> (scale to zero). Returns a human-readable resource summary.</summary>
    Task<string> PromoteAsync(string context, string @namespace, string name, string greenSlot, string oldSlot, CancellationToken cancellationToken = default);

    /// <summary>Roll back: delete the green-slot Deployment. The active slot stays live.</summary>
    Task RollbackAsync(string context, string @namespace, string name, string greenSlot, CancellationToken cancellationToken = default);
}
