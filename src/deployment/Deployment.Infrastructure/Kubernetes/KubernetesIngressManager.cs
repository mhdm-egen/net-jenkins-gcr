using k8s;
using k8s.Autorest;
using k8s.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Deployment.Application.Abstractions;

namespace Deployment.Infrastructure.Kubernetes;

/// <summary>
/// <see cref="IIngressManager"/> over the KubernetesClient. Picks the namespace's frontend Service by a name
/// heuristic and stamps a single Ingress (<c>app-frontend</c>) routing the given host to it, so a preview /
/// app deploy gets a browsable URL on a vanilla cluster (no mesh). Idempotent — replaces an existing Ingress.
/// </summary>
internal sealed class KubernetesIngressManager : IIngressManager
{
    private const string IngressName = "app-frontend";

    // Ordered frontend name hints — earlier wins. Backend/data services are skipped for the primary host.
    private static readonly string[] FrontendHints = { "front", "web", "ui", "gateway", "bff", "portal" };
    private static readonly string[] BackendHints = { "api", "service", "svc", "worker", "db", "cache", "redis", "sql" };

    private readonly IKubeClientFactory _factory;
    private readonly IOptionsMonitor<KubernetesOptions> _options;
    private readonly ILogger<KubernetesIngressManager> _logger;

    public KubernetesIngressManager(IKubeClientFactory factory, IOptionsMonitor<KubernetesOptions> options, ILogger<KubernetesIngressManager> logger)
    {
        _factory = factory;
        _options = options;
        _logger = logger;
    }

    public Task<string?> EnsureFrontendIngressAsync(string? context, string @namespace, string subdomain, CancellationToken cancellationToken = default)
    {
        var opts = _options.CurrentValue;
        if (!opts.PreviewIngressEnabled) return Task.FromResult<string?>(null);
        return EnsureAsync(context, @namespace, subdomain, opts.PreviewIngressDomain, cancellationToken);
    }

    public Task<string?> EnsureAppIngressAsync(string? context, string @namespace, string subdomain, CancellationToken cancellationToken = default)
    {
        var opts = _options.CurrentValue;
        if (!opts.AppIngressEnabled) return Task.FromResult<string?>(null);
        return EnsureAsync(context, @namespace, subdomain, opts.AppIngressDomain, cancellationToken);
    }

    private async Task<string?> EnsureAsync(string? context, string @namespace, string subdomain, string domain, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(@namespace) || string.IsNullOrWhiteSpace(subdomain)) return null;
        var host = $"{subdomain.Trim()}.{domain}";
        using var client = _factory.Create(context);

        var services = await client.CoreV1.ListNamespacedServiceAsync(@namespace, cancellationToken: cancellationToken).ConfigureAwait(false);
        var frontend = PickFrontend(services.Items);
        if (frontend is null)
        {
            _logger.LogWarning("[ingress] No frontend Service found in {Namespace}; skipping ingress for {Host}.", @namespace, host);
            return null;
        }

        var port = PickHttpPort(frontend);
        var ingress = BuildIngress(@namespace, host, frontend.Metadata.Name, port);

        try
        {
            await client.NetworkingV1.ReadNamespacedIngressAsync(IngressName, @namespace, cancellationToken: cancellationToken).ConfigureAwait(false);
            await client.NetworkingV1.ReplaceNamespacedIngressAsync(ingress, IngressName, @namespace, cancellationToken: cancellationToken).ConfigureAwait(false);
        }
        catch (HttpOperationException http) when ((int)http.Response.StatusCode == 404)
        {
            await client.NetworkingV1.CreateNamespacedIngressAsync(ingress, @namespace, cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        var url = $"http://{host}";
        _logger.LogInformation("[ingress] {Namespace}: {Host} -> {Service}:{Port} ({Url}).", @namespace, host, frontend.Metadata.Name, port, url);
        return url;
    }

    public async Task<string?> GetFrontendUrlAsync(string? context, string @namespace, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(@namespace)) return null;
        try
        {
            using var client = _factory.Create(context);
            var ing = await client.NetworkingV1.ReadNamespacedIngressAsync(IngressName, @namespace, cancellationToken: cancellationToken).ConfigureAwait(false);
            var host = ing.Spec?.Rules?.FirstOrDefault(r => !string.IsNullOrWhiteSpace(r.Host))?.Host;
            return string.IsNullOrWhiteSpace(host) ? null : $"http://{host}";
        }
        catch (HttpOperationException http) when ((int)http.Response.StatusCode == 404) { return null; }
        catch { return null; }
    }

    public async Task DeleteIngressAsync(string? context, string @namespace, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(@namespace)) return;
        try
        {
            using var client = _factory.Create(context);
            await client.NetworkingV1.DeleteNamespacedIngressAsync(IngressName, @namespace, cancellationToken: cancellationToken).ConfigureAwait(false);
        }
        catch (HttpOperationException http) when ((int)http.Response.StatusCode == 404) { /* already gone */ }
    }

    /// <summary>Prefer a frontend-looking Service; fall back to the only non-headless Service, else the first.</summary>
    private static V1Service? PickFrontend(IEnumerable<V1Service> services)
    {
        var routable = services
            .Where(s => !string.Equals(s.Spec?.ClusterIP, "None", StringComparison.OrdinalIgnoreCase) && (s.Spec?.Ports?.Count > 0))
            .ToList();
        if (routable.Count == 0) return null;

        foreach (var hint in FrontendHints)
        {
            var hit = routable.FirstOrDefault(s => s.Metadata.Name.Contains(hint, StringComparison.OrdinalIgnoreCase));
            if (hit is not null) return hit;
        }
        // No positive frontend hint: prefer a Service that doesn't look like a backend, else just the first.
        return routable.FirstOrDefault(s => !BackendHints.Any(h => s.Metadata.Name.Contains(h, StringComparison.OrdinalIgnoreCase)))
            ?? routable[0];
    }

    /// <summary>The Service's http port: one named "http", else port 8080/80, else the first.</summary>
    private static int PickHttpPort(V1Service svc)
    {
        var ports = svc.Spec.Ports;
        return (ports.FirstOrDefault(p => string.Equals(p.Name, "http", StringComparison.OrdinalIgnoreCase))
                ?? ports.FirstOrDefault(p => p.Port is 8080 or 80)
                ?? ports[0]).Port;
    }

    private V1Ingress BuildIngress(string @namespace, string host, string serviceName, int port) => new()
    {
        Metadata = new V1ObjectMeta { Name = IngressName, NamespaceProperty = @namespace },
        Spec = new V1IngressSpec
        {
            IngressClassName = _options.CurrentValue.IngressClassName,
            Rules = new List<V1IngressRule>
            {
                new()
                {
                    Host = host,
                    Http = new V1HTTPIngressRuleValue
                    {
                        Paths = new List<V1HTTPIngressPath>
                        {
                            new()
                            {
                                Path = "/",
                                PathType = "Prefix",
                                Backend = new V1IngressBackend
                                {
                                    Service = new V1IngressServiceBackend
                                    {
                                        Name = serviceName,
                                        Port = new V1ServiceBackendPort { Number = port },
                                    },
                                },
                            },
                        },
                    },
                },
            },
        },
    };
}
