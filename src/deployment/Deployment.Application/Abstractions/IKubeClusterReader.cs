using Deployment.Contracts.Kubernetes;

namespace Deployment.Application.Abstractions;

/// <summary>
/// Read-only cluster browsing over the Kubernetes client — the general counterpart to the Aspire-app-scoped
/// <c>IAspireClusterStatusReader</c>. Implemented in Infrastructure. Non-throwing: an unreachable cluster or
/// missing namespace comes back as data (empty / not-reachable), not an exception.
/// </summary>
public interface IKubeClusterReader
{
    /// <summary>Contexts defined in the kubeconfig (the current one flagged).</summary>
    Task<IReadOnlyList<K8sContextDto>> ListContextsAsync(CancellationToken cancellationToken = default);

    /// <summary>Every namespace in the cluster (light — name/phase/age/labels).</summary>
    Task<IReadOnlyList<K8sNamespaceDto>> ListNamespacesAsync(string? context, CancellationToken cancellationToken = default);

    /// <summary>Full read of one namespace: workloads (+pods), Services, Ingresses, overall health.</summary>
    Task<K8sNamespaceDetailDto> GetNamespaceAsync(string? context, string @namespace, CancellationToken cancellationToken = default);

    /// <summary>A tail of a pod's logs (best-effort). <paramref name="container"/> null = the pod's first/only container.</summary>
    Task<PodLogDto> GetPodLogAsync(string? context, string @namespace, string pod, string? container, int tailLines, CancellationToken cancellationToken = default);
}
