using k8s;
using k8s.Autorest;
using Microsoft.Extensions.Logging;
using Deployment.Application.Abstractions;

namespace Deployment.Infrastructure.Kubernetes;

/// <summary><see cref="INamespaceManager"/> over the KubernetesClient — deletes a namespace (and everything in
/// it) for preview teardown. A 404 is treated as success so teardown is idempotent.</summary>
internal sealed class KubernetesNamespaceManager : INamespaceManager
{
    private readonly IKubeClientFactory _factory;
    private readonly ILogger<KubernetesNamespaceManager> _logger;

    public KubernetesNamespaceManager(IKubeClientFactory factory, ILogger<KubernetesNamespaceManager> logger)
    {
        _factory = factory;
        _logger = logger;
    }

    public async Task DeleteNamespaceAsync(string? context, string @namespace, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(@namespace)) return;
        try
        {
            using var client = _factory.Create(context);
            await client.CoreV1.DeleteNamespaceAsync(@namespace, cancellationToken: cancellationToken).ConfigureAwait(false);
            _logger.LogInformation("[k8s] Deleted namespace {Namespace} (context {Context}).", @namespace, context);
        }
        catch (HttpOperationException http) when ((int)http.Response.StatusCode == 404)
        {
            // Already gone.
        }
    }
}
