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

    /// <summary>
    /// Returns a human-readable warning when the URL this manager just handed out cannot actually
    /// serve — i.e. the Ingress exists but no controller backs it — or <c>null</c> when all is well.
    ///
    /// Stamping an Ingress always "succeeds": the object is accepted by the API server whether or not
    /// anything implements its class. On a cluster with no ingress controller the deploy therefore
    /// reports success, the pods run, and the URL fails with ERR_CONNECTION_REFUSED, which looks like
    /// a broken app rather than a missing prerequisite. Callers surface this on the run/preview log so
    /// the cause is visible where the URL is.
    /// </summary>
    Task<string?> DescribeUnbackedIngressAsync(string? context, string @namespace, CancellationToken cancellationToken = default);
}
