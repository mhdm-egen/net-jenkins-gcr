namespace Deployment.Application.Abstractions;

/// <summary>
/// Stamps a Kubernetes Ingress so a deployed app gets a browsable host URL (vanilla-k8s: no mesh).
/// Implemented in Infrastructure over the KubernetesClient. Best-effort: returns the URL it wired up,
/// or <c>null</c> when disabled or no suitable frontend Service was found in the namespace.
/// </summary>
public interface IIngressManager
{
    /// <summary>Ensure an Ingress in <paramref name="namespace"/> routing <c>{subdomain}.{configured-domain}</c>
    /// to the namespace's frontend Service (heuristic). Idempotent. Returns the <c>http://…</c> URL on success,
    /// or <c>null</c> when disabled or no frontend Service was found.</summary>
    Task<string?> EnsureFrontendIngressAsync(string? context, string @namespace, string subdomain, CancellationToken cancellationToken = default);

    /// <summary>Delete the Ingress this manager creates (namespace teardown handles the rest). Idempotent.</summary>
    Task DeleteIngressAsync(string? context, string @namespace, CancellationToken cancellationToken = default);
}
