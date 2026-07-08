namespace Deployment.Application.Abstractions;

/// <summary>Deletes a Kubernetes namespace (preview-environment teardown). Implemented in Infrastructure over
/// the KubernetesClient; a missing namespace is treated as success (idempotent).</summary>
public interface INamespaceManager
{
    Task DeleteNamespaceAsync(string? context, string @namespace, CancellationToken cancellationToken = default);
}
