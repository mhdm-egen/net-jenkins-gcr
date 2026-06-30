namespace Deployment.Domain.Mappings;

/// <summary>
/// Per-mapping inputs for the <see cref="DeploymentStepKind.KubernetesApply"/> step: the platform
/// generates a Deployment (+ optional Service) from these plus the resolved image. The target cluster
/// context + namespace come from the environment. Persisted as JSON on the mapping.
/// </summary>
public sealed record KubernetesSpec(
    string DeploymentName,
    int ContainerPort,
    int Replicas,
    IReadOnlyDictionary<string, string> EnvVars,
    string? ImagePullSecret,
    bool CreateService)
{
    public static KubernetesSpec Default(string deploymentName) =>
        new(deploymentName, 8080, 1, new Dictionary<string, string>(), null, true);
}
