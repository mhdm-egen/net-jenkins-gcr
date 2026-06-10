namespace Deployment.Domain.Deployments;

/// <summary>
/// Metadata only — the model tracks the chosen rollout strategy and the
/// health outcome observed; it does not orchestrate canary mechanics or
/// traffic shifts (decisions §6.1). Adapter primitives (K8s, App Service
/// slots, Container Apps revisions) own the actual rollout.
/// </summary>
public enum DeploymentStrategy
{
    Direct = 0,
    BlueGreen = 1,
    Canary = 2,
    Rolling = 3,
}
