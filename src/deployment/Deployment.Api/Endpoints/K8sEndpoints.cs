using Deployment.Application.Abstractions;

namespace Deployment.Api.Endpoints;

/// <summary>
/// Read-only Kubernetes cluster browsing for the web-admin's K8s screens. Backed by <see cref="IKubeClusterReader"/>
/// (injected directly — these are pure reads with no cross-reader composition). <c>context</c> is an optional query
/// param; null means the kubeconfig's current context.
/// </summary>
public static class K8sEndpoints
{
    public static IEndpointRouteBuilder MapK8sEndpoints(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/deployment/k8s").WithTags("Kubernetes");

        g.MapGet("contexts", async (IKubeClusterReader reader, CancellationToken ct) =>
            Results.Ok(await reader.ListContextsAsync(ct)));

        g.MapGet("namespaces", async (string? context, IKubeClusterReader reader, CancellationToken ct) =>
            Results.Ok(await reader.ListNamespacesAsync(context, ct)));

        g.MapGet("namespaces/{ns}", async (string ns, string? context, IKubeClusterReader reader, CancellationToken ct) =>
            Results.Ok(await reader.GetNamespaceAsync(context, ns, ct)));

        g.MapGet("namespaces/{ns}/pods/{pod}/logs", async (
            string ns, string pod, string? context, string? container, int? tail, IKubeClusterReader reader, CancellationToken ct) =>
            Results.Ok(await reader.GetPodLogAsync(context, ns, pod, container, tail ?? 500, ct)));

        return app;
    }
}
