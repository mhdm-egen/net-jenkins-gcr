using System.Text;
using k8s;
using k8s.Autorest;
using k8s.KubeConfigModels;
using k8s.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Deployment.Application.Abstractions;
using Deployment.Application.Features.AspireApps;
using Deployment.Contracts.Kubernetes;

namespace Deployment.Infrastructure.Kubernetes;

/// <summary>
/// <see cref="IKubeClusterReader"/> — read-only cluster browsing over the KubernetesClient. Workloads (+pods,
/// health) are delegated to <see cref="IAspireClusterStatusReader"/> (single source of that logic); this adds
/// cluster-wide namespace/context listing, Services + Ingresses, and pod-log tails. All reads are non-throwing.
/// </summary>
internal sealed class KubeClusterReader : IKubeClusterReader
{
    private readonly IKubeClientFactory _factory;
    private readonly IAspireClusterStatusReader _status;
    private readonly IOptionsMonitor<KubernetesOptions> _options;
    private readonly ILogger<KubeClusterReader> _logger;

    public KubeClusterReader(
        IKubeClientFactory factory, IAspireClusterStatusReader status,
        IOptionsMonitor<KubernetesOptions> options, ILogger<KubeClusterReader> logger)
    {
        _factory = factory;
        _status = status;
        _options = options;
        _logger = logger;
    }

    public Task<IReadOnlyList<K8sContextDto>> ListContextsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var path = _options.CurrentValue.Kubeconfig;
            K8SConfiguration cfg = string.IsNullOrWhiteSpace(path)
                ? KubernetesClientConfiguration.LoadKubeConfig()
                : KubernetesClientConfiguration.LoadKubeConfig(path);
            var current = cfg.CurrentContext;
            var list = (cfg.Contexts ?? Enumerable.Empty<Context>())
                .Where(c => !string.IsNullOrWhiteSpace(c.Name))
                .Select(c => new K8sContextDto(c.Name, string.Equals(c.Name, current, StringComparison.Ordinal)))
                .ToList();
            return Task.FromResult<IReadOnlyList<K8sContextDto>>(list);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[k8s] Failed to load kubeconfig contexts.");
            return Task.FromResult<IReadOnlyList<K8sContextDto>>(Array.Empty<K8sContextDto>());
        }
    }

    public async Task<IReadOnlyList<K8sNamespaceDto>> ListNamespacesAsync(string? context, CancellationToken cancellationToken = default)
    {
        try
        {
            using var client = _factory.Create(context);
            var namespaces = await client.CoreV1.ListNamespaceAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
            return namespaces.Items
                .Select(n => new K8sNamespaceDto(
                    n.Metadata?.Name ?? "(unnamed)",
                    n.Status?.Phase,
                    n.Metadata?.CreationTimestamp,
                    n.Metadata?.Labels is { } l ? new Dictionary<string, string>(l) : null))
                .OrderBy(n => n.Name, StringComparer.Ordinal)
                .ToList();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[k8s] Failed to list namespaces (context {Context}).", context);
            return Array.Empty<K8sNamespaceDto>();
        }
    }

    public async Task<K8sNamespaceDetailDto> GetNamespaceAsync(string? context, string @namespace, CancellationToken cancellationToken = default)
    {
        // Workloads + pods + health: reuse the Aspire status reader (same computation, already non-throwing).
        var cluster = await _status.GetAsync(context, @namespace, cancellationToken).ConfigureAwait(false);

        var services = Array.Empty<K8sServiceDto>() as IReadOnlyList<K8sServiceDto>;
        var ingresses = Array.Empty<K8sIngressDto>() as IReadOnlyList<K8sIngressDto>;
        if (cluster.Reachable)
        {
            try
            {
                using var client = _factory.Create(context);
                var svc = await client.CoreV1.ListNamespacedServiceAsync(@namespace, cancellationToken: cancellationToken).ConfigureAwait(false);
                services = svc.Items.Select(ToServiceDto).OrderBy(s => s.Name, StringComparer.Ordinal).ToList();
                var ing = await client.NetworkingV1.ListNamespacedIngressAsync(@namespace, cancellationToken: cancellationToken).ConfigureAwait(false);
                ingresses = ing.Items.Select(ToIngressDto).OrderBy(i => i.Name, StringComparer.Ordinal).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[k8s] Failed to read services/ingresses for {Namespace} (context {Context}).", @namespace, context);
            }
        }

        return new K8sNamespaceDetailDto(
            @namespace, cluster.Reachable, cluster.Error, cluster.OverallHealth, cluster.Workloads, services, ingresses);
    }

    public async Task<PodLogDto> GetPodLogAsync(string? context, string @namespace, string pod, string? container, int tailLines, CancellationToken cancellationToken = default)
    {
        var tail = tailLines <= 0 ? 500 : Math.Min(tailLines, 5000);
        try
        {
            using var client = _factory.Create(context);

            // Multi-container pods require a container name; default to the first if unspecified.
            if (string.IsNullOrWhiteSpace(container))
            {
                try
                {
                    var p = await client.CoreV1.ReadNamespacedPodAsync(pod, @namespace, cancellationToken: cancellationToken).ConfigureAwait(false);
                    container = p.Spec?.Containers?.FirstOrDefault()?.Name;
                }
                catch { /* fall through — let the log call pick the default */ }
            }

            using var stream = await client.CoreV1.ReadNamespacedPodLogAsync(
                pod, @namespace, container: container, tailLines: tail, cancellationToken: cancellationToken).ConfigureAwait(false);
            using var reader = new StreamReader(stream, Encoding.UTF8);
            var text = await reader.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
            return new PodLogDto(pod, container, string.IsNullOrWhiteSpace(text) ? "(no log output)" : text);
        }
        catch (HttpOperationException http) when ((int)http.Response.StatusCode == 404)
        {
            return new PodLogDto(pod, container, $"Pod '{pod}' not found in namespace '{@namespace}'.");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[k8s] Failed to read logs for pod {Pod} in {Namespace}.", pod, @namespace);
            return new PodLogDto(pod, container, $"Could not read logs: {FirstLine(ex.Message)}");
        }
    }

    private static K8sServiceDto ToServiceDto(V1Service s)
    {
        var ports = (s.Spec?.Ports ?? new List<V1ServicePort>())
            .Select(p =>
            {
                var name = string.IsNullOrWhiteSpace(p.Name) ? "" : $"{p.Name}:";
                var target = p.TargetPort?.Value is { Length: > 0 } t ? t : p.Port.ToString();
                return $"{name}{p.Port}→{target}/{p.Protocol ?? "TCP"}";
            })
            .ToList();
        return new K8sServiceDto(
            s.Metadata?.Name ?? "(unnamed)",
            s.Spec?.Type ?? "ClusterIP",
            s.Spec?.ClusterIP,
            ports);
    }

    private static K8sIngressDto ToIngressDto(V1Ingress ing)
    {
        var tlsHosts = (ing.Spec?.Tls ?? new List<V1IngressTLS>())
            .SelectMany(t => t.Hosts ?? new List<string>())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var rules = ing.Spec?.Rules ?? new List<V1IngressRule>();
        var hosts = rules.Where(r => !string.IsNullOrWhiteSpace(r.Host)).Select(r => r.Host!).Distinct(StringComparer.Ordinal).ToList();
        var urls = hosts.Select(h => $"{(tlsHosts.Contains(h) ? "https" : "http")}://{h}").ToList();

        var backends = new List<string>();
        foreach (var r in rules)
        {
            foreach (var p in r.Http?.Paths ?? new List<V1HTTPIngressPath>())
            {
                var svc = p.Backend?.Service;
                if (svc is null) continue;
                var port = svc.Port?.Number?.ToString() ?? svc.Port?.Name ?? "?";
                backends.Add($"{r.Host}{p.Path} → {svc.Name}:{port}");
            }
        }

        return new K8sIngressDto(ing.Metadata?.Name ?? "(unnamed)", ing.Spec?.IngressClassName, hosts, urls, backends);
    }

    private static string FirstLine(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return "no detail";
        var nl = s.IndexOfAny(['\r', '\n']);
        return (nl >= 0 ? s[..nl] : s).Trim();
    }
}
