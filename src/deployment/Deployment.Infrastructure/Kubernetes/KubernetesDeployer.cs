using k8s;
using k8s.Autorest;
using k8s.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Deployment.Application.Abstractions;
using Deployment.Domain.Runs;

namespace Deployment.Infrastructure.Kubernetes;

public sealed record KubernetesApplyRequest(
    string Context, string Namespace, string DeploymentName, string Image, int ContainerPort, int Replicas,
    IReadOnlyDictionary<string, string> EnvVars, string? ImagePullSecret, bool CreateService);

/// <summary>Applies a generated Deployment (+ optional Service) for a single service to a cluster and waits for ready.</summary>
public interface IKubernetesDeployer
{
    Task<string> ApplyAsync(KubernetesApplyRequest request, CancellationToken cancellationToken = default);
}

/// <summary>
/// <see cref="IKubernetesDeployer"/> over the KubernetesClient .NET API: server-side-applies a namespace,
/// Deployment, and optional Service generated from the request, then polls until the Deployment's available
/// replicas reach the desired count. Failures map to a categorized <see cref="DeploymentStepException"/>.
/// </summary>
internal sealed class KubernetesDeployer : IKubernetesDeployer
{
    private const string FieldManager = "cicd-deployment";

    private readonly IKubeClientFactory _factory;
    private readonly IOptionsMonitor<KubernetesOptions> _options;
    private readonly ILogger<KubernetesDeployer> _logger;

    public KubernetesDeployer(IKubeClientFactory factory, IOptionsMonitor<KubernetesOptions> options, ILogger<KubernetesDeployer> logger)
    {
        _factory = factory;
        _options = options;
        _logger = logger;
    }

    public async Task<string> ApplyAsync(KubernetesApplyRequest req, CancellationToken cancellationToken = default)
    {
        var labels = new Dictionary<string, string> { ["app"] = req.DeploymentName, ["app.kubernetes.io/managed-by"] = FieldManager };
        try
        {
            using var client = _factory.Create(req.Context);

            await ServerSideApplyAsync(
                () => client.CoreV1.PatchNamespaceAsync(Apply(new V1Namespace
                {
                    ApiVersion = "v1", Kind = "Namespace",
                    Metadata = new V1ObjectMeta { Name = req.Namespace },
                }), req.Namespace, fieldManager: FieldManager, force: true, cancellationToken: cancellationToken)).ConfigureAwait(false);

            var deployment = BuildDeployment(req, labels);
            await client.AppsV1.PatchNamespacedDeploymentAsync(Apply(deployment), req.DeploymentName, req.Namespace,
                fieldManager: FieldManager, force: true, cancellationToken: cancellationToken).ConfigureAwait(false);

            if (req.CreateService)
            {
                var service = BuildService(req, labels);
                await client.CoreV1.PatchNamespacedServiceAsync(Apply(service), req.DeploymentName, req.Namespace,
                    fieldManager: FieldManager, force: true, cancellationToken: cancellationToken).ConfigureAwait(false);
            }

            _logger.LogInformation("[k8s] Applied deployment {Name} to {Namespace} (context {Context}).", req.DeploymentName, req.Namespace, req.Context);
            await WaitForReadyAsync(client, req, cancellationToken).ConfigureAwait(false);
            return $"deployment/{req.DeploymentName} in {req.Namespace}";
        }
        catch (DeploymentStepException) { throw; }
        catch (HttpOperationException http)
        {
            var kind = (int)http.Response.StatusCode switch
            {
                401 or 403 => StepFailureKind.CloudRunAuth, // generic auth bucket
                404 => StepFailureKind.Config,
                _ => StepFailureKind.Unknown,
            };
            throw new DeploymentStepException(kind, $"Kubernetes apply failed ({(int)http.Response.StatusCode}): {FirstLine(http.Response.Content)}", http);
        }
        catch (Exception ex)
        {
            throw new DeploymentStepException(StepFailureKind.Unknown, $"Kubernetes apply failed: {ex.Message}", ex);
        }
    }

    private static V1Deployment BuildDeployment(KubernetesApplyRequest req, IDictionary<string, string> labels) => new()
    {
        ApiVersion = "apps/v1", Kind = "Deployment",
        Metadata = new V1ObjectMeta { Name = req.DeploymentName, NamespaceProperty = req.Namespace, Labels = labels },
        Spec = new V1DeploymentSpec
        {
            Replicas = Math.Max(1, req.Replicas),
            Selector = new V1LabelSelector { MatchLabels = new Dictionary<string, string> { ["app"] = req.DeploymentName } },
            Template = new V1PodTemplateSpec
            {
                Metadata = new V1ObjectMeta { Labels = labels },
                Spec = new V1PodSpec
                {
                    Containers = new List<V1Container>
                    {
                        new()
                        {
                            Name = req.DeploymentName,
                            Image = req.Image,
                            Ports = new List<V1ContainerPort> { new() { ContainerPort = req.ContainerPort } },
                            Env = req.EnvVars.Select(kv => new V1EnvVar { Name = kv.Key, Value = kv.Value }).ToList(),
                        },
                    },
                    ImagePullSecrets = string.IsNullOrWhiteSpace(req.ImagePullSecret)
                        ? null
                        : new List<V1LocalObjectReference> { new() { Name = req.ImagePullSecret } },
                },
            },
        },
    };

    private static V1Service BuildService(KubernetesApplyRequest req, IDictionary<string, string> labels) => new()
    {
        ApiVersion = "v1", Kind = "Service",
        Metadata = new V1ObjectMeta { Name = req.DeploymentName, NamespaceProperty = req.Namespace, Labels = labels },
        Spec = new V1ServiceSpec
        {
            Selector = new Dictionary<string, string> { ["app"] = req.DeploymentName },
            Ports = new List<V1ServicePort> { new() { Port = req.ContainerPort, TargetPort = req.ContainerPort } },
        },
    };

    private async Task WaitForReadyAsync(IKubernetes client, KubernetesApplyRequest req, CancellationToken ct)
    {
        var o = _options.CurrentValue;
        var deadline = DateTimeOffset.UtcNow.AddSeconds(Math.Max(1, o.ReadinessTimeoutSeconds));
        var desired = Math.Max(1, req.Replicas);
        while (DateTimeOffset.UtcNow < deadline)
        {
            var status = (await client.AppsV1.ReadNamespacedDeploymentStatusAsync(req.DeploymentName, req.Namespace, cancellationToken: ct).ConfigureAwait(false)).Status;
            if ((status?.AvailableReplicas ?? 0) >= desired) return;
            await Task.Delay(TimeSpan.FromSeconds(Math.Max(1, o.ReadinessPollSeconds)), ct).ConfigureAwait(false);
        }
        throw new DeploymentStepException(StepFailureKind.Timeout,
            $"Deployment '{req.DeploymentName}' did not reach {desired} available replica(s) within {o.ReadinessTimeoutSeconds}s.");
    }

    private static V1Patch Apply(object body) => new(body, V1Patch.PatchType.ApplyPatch);

    private static async Task ServerSideApplyAsync(Func<Task> apply)
    {
        // Namespace may already exist and be owned by another manager — that's fine.
        try { await apply().ConfigureAwait(false); }
        catch (HttpOperationException e) when ((int)e.Response.StatusCode is 409) { /* already exists */ }
    }

    private static string FirstLine(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return "no detail";
        var nl = s.IndexOfAny(['\r', '\n']);
        return (nl >= 0 ? s[..nl] : s).Trim();
    }
}
