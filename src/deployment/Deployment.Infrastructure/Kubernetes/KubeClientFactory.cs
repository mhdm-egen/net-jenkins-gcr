using k8s;
using Microsoft.Extensions.Options;

namespace Deployment.Infrastructure.Kubernetes;

/// <summary>
/// Builds an <see cref="IKubernetes"/> client for a given context. This interface is the credential-source
/// seam: today it loads a kubeconfig; a GKE-via-ADC implementation is a drop-in replacement later.
/// </summary>
public interface IKubeClientFactory
{
    IKubernetes Create(string? context);
}

internal sealed class KubeClientFactory : IKubeClientFactory
{
    private readonly IOptionsMonitor<KubernetesOptions> _options;
    public KubeClientFactory(IOptionsMonitor<KubernetesOptions> options) => _options = options;

    public IKubernetes Create(string? context)
    {
        var path = _options.CurrentValue.Kubeconfig;
        var config = KubernetesClientConfiguration.BuildConfigFromConfigFile(
            kubeconfigPath: string.IsNullOrWhiteSpace(path) ? null : path,
            currentContext: string.IsNullOrWhiteSpace(context) ? null : context);
        return new k8s.Kubernetes(config);
    }
}
