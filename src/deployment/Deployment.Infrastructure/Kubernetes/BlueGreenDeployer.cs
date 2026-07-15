using k8s;
using k8s.Autorest;
using k8s.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Deployment.Application.Abstractions;
using Deployment.Domain.Runs;

namespace Deployment.Infrastructure.Kubernetes;

/// <summary>
/// <see cref="IBlueGreenDeployer"/> over the KubernetesClient .NET API. Models blue-green with two
/// Deployments (<c>{name}-blue</c>/<c>{name}-green</c>) sharing a Service that selects one slot; the
/// selector swap is the traffic cutover. Reuses the SSA + readiness-poll patterns from
/// <see cref="KubernetesDeployer"/>.
/// </summary>
internal sealed class BlueGreenDeployer : IBlueGreenDeployer
{
    private const string FieldManager = "cicd-deployment";
    private const string Blue = "blue";
    private const string Green = "green";

    private readonly IKubeClientFactory _factory;
    private readonly IOptionsMonitor<KubernetesOptions> _options;
    private readonly ILogger<BlueGreenDeployer> _logger;

    public BlueGreenDeployer(IKubeClientFactory factory, IOptionsMonitor<KubernetesOptions> options, ILogger<BlueGreenDeployer> logger)
    {
        _factory = factory;
        _options = options;
        _logger = logger;
    }

    public async Task<BlueGreenDeployResult> DeployGreenAsync(BlueGreenDeployRequest req, CancellationToken ct = default)
    {
        try
        {
            using var client = _factory.Create(req.Context);

            await ServerSideApplyAsync(() => client.CoreV1.PatchNamespaceAsync(
                Apply(new V1Namespace { ApiVersion = "v1", Kind = "Namespace", Metadata = new V1ObjectMeta { Name = req.Namespace } }),
                req.Namespace, fieldManager: FieldManager, force: true, cancellationToken: ct)).ConfigureAwait(false);

            var active = await ResolveActiveSlotAsync(client, req.Namespace, req.Name, ct).ConfigureAwait(false);
            var target = active is null ? Blue : (active == Blue ? Green : Blue);

            // Apply the target-slot Deployment ({name}-{slot}) with slot labels.
            await client.AppsV1.PatchNamespacedDeploymentAsync(
                Apply(BuildDeployment(req, target)), SlotName(req.Name, target), req.Namespace,
                fieldManager: FieldManager, force: true, cancellationToken: ct).ConfigureAwait(false);

            // First deploy: no Service yet → create it selecting the new slot (it becomes active immediately).
            if (active is null)
            {
                await client.CoreV1.PatchNamespacedServiceAsync(
                    Apply(BuildService(req, target)), req.Name, req.Namespace,
                    fieldManager: FieldManager, force: true, cancellationToken: ct).ConfigureAwait(false);
            }

            var healthy = await HealthGateAsync(client, req, target, ct).ConfigureAwait(false);
            _logger.LogInformation("[k8s] blue-green deploy {Name} slot={Slot} (active={Active}) healthy={Healthy}.",
                req.Name, target, active ?? target, healthy);
            return new BlueGreenDeployResult(target, active ?? target, healthy,
                $"deployment/{SlotName(req.Name, target)} ({(healthy ? "healthy" : "unhealthy")})");
        }
        catch (DeploymentStepException) { throw; }
        catch (HttpOperationException http)
        {
            throw new DeploymentStepException(MapHttp(http), $"blue-green deploy failed ({(int)http.Response.StatusCode}): {FirstLine(http.Response.Content)}", http);
        }
        catch (Exception ex)
        {
            throw new DeploymentStepException(StepFailureKind.Unknown, $"blue-green deploy failed: {ex.Message}", ex);
        }
    }

    public async Task<string> PromoteAsync(string context, string ns, string name, string greenSlot, string oldSlot, CancellationToken ct = default)
    {
        using var client = _factory.Create(context);

        // Cut traffic over: merge-patch only the Service selector (a server-side apply would strip the
        // ports it didn't restate; a JSON merge patch leaves ports and everything else untouched).
        await client.CoreV1.PatchNamespacedServiceAsync(
            new V1Patch($"{{\"spec\":{{\"selector\":{{\"app\":\"{name}\",\"slot\":\"{greenSlot}\"}}}}}}", V1Patch.PatchType.MergePatch),
            name, ns, cancellationToken: ct).ConfigureAwait(false);

        // Retire the old slot (delete, or scale to zero for a fast re-promote).
        if (!string.IsNullOrWhiteSpace(oldSlot) && oldSlot != greenSlot)
        {
            var old = SlotName(name, oldSlot);
            if (_options.CurrentValue.RolloutDeleteRetiredSlot)
                await IgnoreNotFoundAsync(() => client.AppsV1.DeleteNamespacedDeploymentAsync(old, ns, cancellationToken: ct)).ConfigureAwait(false);
            else
                await IgnoreNotFoundAsync(() => client.AppsV1.PatchNamespacedDeploymentScaleAsync(
                    new V1Patch("{\"spec\":{\"replicas\":0}}", V1Patch.PatchType.MergePatch), old, ns, cancellationToken: ct)).ConfigureAwait(false);
        }

        _logger.LogInformation("[k8s] blue-green promote {Name} -> slot={Slot} (retired {Old}).", name, greenSlot, oldSlot);
        return $"service/{name} -> deployment/{SlotName(name, greenSlot)}";
    }

    public async Task RollbackAsync(string context, string ns, string name, string greenSlot, CancellationToken ct = default)
    {
        using var client = _factory.Create(context);
        await IgnoreNotFoundAsync(() => client.AppsV1.DeleteNamespacedDeploymentAsync(SlotName(name, greenSlot), ns, cancellationToken: ct)).ConfigureAwait(false);
        _logger.LogInformation("[k8s] blue-green rollback {Name}: deleted slot={Slot}.", name, greenSlot);
    }

    /// <summary>The slot the Service currently routes to, or null if there's no Service yet.</summary>
    private static async Task<string?> ResolveActiveSlotAsync(IKubernetes client, string ns, string name, CancellationToken ct)
    {
        try
        {
            var svc = await client.CoreV1.ReadNamespacedServiceAsync(name, ns, cancellationToken: ct).ConfigureAwait(false);
            return svc.Spec?.Selector is { } sel && sel.TryGetValue("slot", out var slot) && !string.IsNullOrWhiteSpace(slot) ? slot : null;
        }
        catch (HttpOperationException e) when ((int)e.Response.StatusCode == 404) { return null; }
    }

    private async Task<bool> HealthGateAsync(IKubernetes client, BlueGreenDeployRequest req, string slot, CancellationToken ct)
    {
        var o = _options.CurrentValue;
        var timeout = o.RolloutHealthTimeoutSeconds > 0 ? o.RolloutHealthTimeoutSeconds : Math.Max(1, o.ReadinessTimeoutSeconds);
        var deadline = DateTimeOffset.UtcNow.AddSeconds(timeout);
        var desired = Math.Max(1, req.Replicas);
        var deployName = SlotName(req.Name, slot);
        var threshold = Math.Max(1, o.RolloutRestartThreshold);

        while (DateTimeOffset.UtcNow < deadline)
        {
            var status = (await client.AppsV1.ReadNamespacedDeploymentStatusAsync(deployName, req.Namespace, cancellationToken: ct).ConfigureAwait(false)).Status;
            if ((status?.AvailableReplicas ?? 0) >= desired) return true;

            // Fail fast on a crash-looping green pod rather than waiting out the whole deadline.
            var pods = await client.CoreV1.ListNamespacedPodAsync(req.Namespace, labelSelector: $"app={req.Name},slot={slot}", cancellationToken: ct).ConfigureAwait(false);
            if (pods.Items.Any(p => (p.Status?.ContainerStatuses?.Sum(c => c.RestartCount) ?? 0) >= threshold))
            {
                _logger.LogWarning("[k8s] blue-green health gate: {Deploy} has a crash-looping pod (>= {Threshold} restarts).", deployName, threshold);
                return false;
            }
            await Task.Delay(TimeSpan.FromSeconds(Math.Max(1, o.ReadinessPollSeconds)), ct).ConfigureAwait(false);
        }
        return false;
    }

    private static V1Deployment BuildDeployment(BlueGreenDeployRequest req, string slot)
    {
        var labels = new Dictionary<string, string>
        {
            ["app"] = req.Name,
            ["slot"] = slot,
            ["app.kubernetes.io/managed-by"] = FieldManager,
        };
        return new V1Deployment
        {
            ApiVersion = "apps/v1", Kind = "Deployment",
            Metadata = new V1ObjectMeta { Name = SlotName(req.Name, slot), NamespaceProperty = req.Namespace, Labels = labels },
            Spec = new V1DeploymentSpec
            {
                Replicas = Math.Max(1, req.Replicas),
                Selector = new V1LabelSelector { MatchLabels = new Dictionary<string, string> { ["app"] = req.Name, ["slot"] = slot } },
                Template = new V1PodTemplateSpec
                {
                    Metadata = new V1ObjectMeta { Labels = labels },
                    Spec = new V1PodSpec
                    {
                        Containers = new List<V1Container>
                        {
                            new()
                            {
                                Name = req.Name,
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
    }

    private static V1Service BuildService(BlueGreenDeployRequest req, string activeSlot) => new()
    {
        ApiVersion = "v1", Kind = "Service",
        Metadata = new V1ObjectMeta { Name = req.Name, NamespaceProperty = req.Namespace, Labels = new Dictionary<string, string> { ["app"] = req.Name } },
        Spec = new V1ServiceSpec
        {
            Selector = new Dictionary<string, string> { ["app"] = req.Name, ["slot"] = activeSlot },
            Ports = new List<V1ServicePort> { new() { Port = req.ContainerPort, TargetPort = req.ContainerPort } },
        },
    };

    private static string SlotName(string name, string slot) => $"{name}-{slot}";
    private static V1Patch Apply(object body) => new(body, V1Patch.PatchType.ApplyPatch);

    private static async Task ServerSideApplyAsync(Func<Task> apply)
    {
        try { await apply().ConfigureAwait(false); }
        catch (HttpOperationException e) when ((int)e.Response.StatusCode is 409) { }
    }

    private static async Task IgnoreNotFoundAsync(Func<Task> op)
    {
        try { await op().ConfigureAwait(false); }
        catch (HttpOperationException e) when ((int)e.Response.StatusCode is 404) { }
    }

    private static StepFailureKind MapHttp(HttpOperationException http) => (int)http.Response.StatusCode switch
    {
        401 or 403 => StepFailureKind.CloudRunAuth,
        404 => StepFailureKind.Config,
        _ => StepFailureKind.Unknown,
    };

    private static string FirstLine(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return "no detail";
        var nl = s.IndexOfAny(['\r', '\n']);
        return (nl >= 0 ? s[..nl] : s).Trim();
    }
}
