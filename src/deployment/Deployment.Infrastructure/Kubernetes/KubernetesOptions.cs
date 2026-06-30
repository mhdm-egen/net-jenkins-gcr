namespace Deployment.Infrastructure.Kubernetes;

/// <summary>
/// Options for the per-service KubernetesApply target. Bound from <c>Deployment:Kubernetes</c>. Cluster
/// access is via a kubeconfig + the environment's context (a GKE-via-ADC factory is a future drop-in).
/// </summary>
public sealed class KubernetesOptions
{
    public const string SectionName = "Deployment:Kubernetes";

    /// <summary>Kubeconfig path; empty = the process default (KUBECONFIG / ~/.kube/config).</summary>
    public string Kubeconfig { get; set; } = string.Empty;

    public int ReadinessTimeoutSeconds { get; set; } = 180;
    public int ReadinessPollSeconds { get; set; } = 3;
}
