namespace Deployment.Application.Abstractions;

/// <summary>
/// Stamps a Kubernetes Ingress so a deployed app gets a browsable host URL (vanilla-k8s: no mesh).
/// Implemented in Infrastructure over the KubernetesClient. Best-effort: returns the URL it wired up,
/// or <c>null</c> when disabled or no suitable frontend Service was found in the namespace.
/// </summary>
public interface IIngressManager
{
    /// <summary>Ensure an Ingress in <paramref name="namespace"/> routing <c>{subdomain}.{preview-domain}</c>
    /// to the namespace's frontend Service (heuristic). Idempotent. Returns the <c>http://…</c> URL on success,
    /// or <c>null</c> when disabled or no frontend Service was found. Used by the preview path.</summary>
    Task<string?> EnsureFrontendIngressAsync(string? context, string @namespace, string subdomain, CancellationToken cancellationToken = default);

    /// <summary>Same as <see cref="EnsureFrontendIngressAsync"/> but under the app-ingress domain — used by the
    /// normal (Direct) Aspire app deploy path so a deployed app gets a browsable URL.</summary>
    Task<string?> EnsureAppIngressAsync(string? context, string @namespace, string subdomain, CancellationToken cancellationToken = default);

    /// <summary>Read back the URL of the Ingress this manager stamps in <paramref name="namespace"/>, or
    /// <c>null</c> if none exists. Cheap (single ingress read) — used to surface an app's live URL.</summary>
    Task<string?> GetFrontendUrlAsync(string? context, string @namespace, CancellationToken cancellationToken = default);

    /// <summary>Delete the Ingress this manager creates (namespace teardown handles the rest). Idempotent.</summary>
    Task DeleteIngressAsync(string? context, string @namespace, CancellationToken cancellationToken = default);
}
