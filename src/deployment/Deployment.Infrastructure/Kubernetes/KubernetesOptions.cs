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

    /// <summary>Blue-green: how long the green slot has to reach its desired ready replicas before the
    /// rollout auto-rolls back. Defaults to <see cref="ReadinessTimeoutSeconds"/> when unset (0).</summary>
    public int RolloutHealthTimeoutSeconds { get; set; }

    /// <summary>Blue-green: a green pod with at least this many restarts fails the health gate early.</summary>
    public int RolloutRestartThreshold { get; set; } = 3;

    /// <summary>Blue-green: when true, delete the retired (old) slot Deployment on promote; otherwise scale
    /// it to zero (kept for a fast manual re-promote). Default false (scale to zero).</summary>
    public bool RolloutDeleteRetiredSlot { get; set; }

    /// <summary>The IngressClass used when stamping a browsable Ingress for a deploy. Default <c>nginx</c>.</summary>
    public string IngressClassName { get; set; } = "nginx";

    /// <summary>DNS suffix for preview URLs — a preview keyed <c>feature-x</c> gets
    /// <c>feature-x.{PreviewIngressDomain}</c>. Default <c>preview.localtest.me</c> (a public wildcard that
    /// resolves to 127.0.0.1, so it works against a localhost ingress with no hosts-file edits).</summary>
    public string PreviewIngressDomain { get; set; } = "preview.localtest.me";

    /// <summary>When false, no Ingress is stamped for previews (they stay ClusterIP / port-forward only).</summary>
    public bool PreviewIngressEnabled { get; set; } = true;
}
