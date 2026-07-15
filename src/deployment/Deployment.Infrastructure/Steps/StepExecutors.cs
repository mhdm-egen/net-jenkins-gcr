using Deployment.Application.Abstractions;
using Deployment.Domain.Mappings;
using Deployment.Domain.Runs;
using Deployment.Infrastructure.Kubernetes;

namespace Deployment.Infrastructure.Steps;

/// <summary>
/// GarPush: copy the source Nexus image into Google Artifact Registry (digest-preserving), and set
/// the run's RemoteImageRef to the GAR reference the next step deploys.
/// Target: <c>{region}-docker.pkg.dev/{project}/{garRepo}/{containerLeaf}</c> (digest or :version).
/// </summary>
internal sealed class GarPushStepExecutor : IDeploymentStepExecutor
{
    private readonly IArtifactPromoter _promoter;
    public GarPushStepExecutor(IArtifactPromoter promoter) => _promoter = promoter;

    public DeploymentStepKind Kind => DeploymentStepKind.GarPush;

    public async Task<StepOutcome> ExecuteAsync(DeploymentContext ctx, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(ctx.SourceRef))
            return StepOutcome.Fail("no source image reference (the container has no Nexus ref).");
        if (string.IsNullOrWhiteSpace(ctx.GarRepository))
            return StepOutcome.Fail("environment has no GAR repository configured.");

        var leaf = LeafName(ctx.ContainerName);
        var (sep, suffix) = SourceSuffix(ctx.SourceRef, ctx.Version);
        var garRef = $"{ctx.Region}-docker.pkg.dev/{ctx.GcpProject}/{ctx.GarRepository.Trim('/')}/{leaf}{sep}{suffix}";

        await _promoter.EnsureCopiedAsync(ctx.SourceRef, garRef, ct).ConfigureAwait(false);
        ctx.RemoteImageRef = garRef;
        return StepOutcome.Ok($"copied to {garRef}");
    }

    private static string LeafName(string containerName)
    {
        var n = containerName.Trim().Trim('/');
        var slash = n.LastIndexOf('/');
        return slash >= 0 ? n[(slash + 1)..] : n;
    }

    /// <summary>Preserve the digest when the source is digest-pinned; otherwise tag by version (or latest).</summary>
    private static (string Sep, string Suffix) SourceSuffix(string sourceRef, string version)
    {
        var at = sourceRef.IndexOf("@sha256:", StringComparison.OrdinalIgnoreCase);
        if (at >= 0) return ("@", sourceRef[(at + 1)..]);
        var tag = string.IsNullOrWhiteSpace(version) ? "latest" : version.Trim();
        return (":", tag);
    }
}

/// <summary>CloudRunDeploy: deploy the promoted image (or the source if no GarPush ran) to Cloud Run.</summary>
internal sealed class CloudRunDeployStepExecutor : IDeploymentStepExecutor
{
    private readonly ICloudRunDeployer _deployer;
    public CloudRunDeployStepExecutor(ICloudRunDeployer deployer) => _deployer = deployer;

    public DeploymentStepKind Kind => DeploymentStepKind.CloudRunDeploy;

    public async Task<StepOutcome> ExecuteAsync(DeploymentContext ctx, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(ctx.CloudRunServiceName))
            return StepOutcome.Fail("mapping has no Cloud Run service name.");
        if (string.IsNullOrWhiteSpace(ctx.GcpProject) || string.IsNullOrWhiteSpace(ctx.Region))
            return StepOutcome.Fail("environment is missing GCP project/region.");

        var revision = await _deployer.DeployAsync(
            new CloudRunDeployRequest(ctx.GcpProject, ctx.Region, ctx.CloudRunServiceName!, ctx.ImageToDeploy), ct).ConfigureAwait(false);
        ctx.CloudRunRevision = revision;
        return StepOutcome.Ok($"deployed revision {revision}");
    }
}

/// <summary>KubernetesApply: deploy the image (GAR-promoted if a GarPush ran, else the Nexus source) to a cluster.</summary>
internal sealed class KubernetesApplyStepExecutor : IDeploymentStepExecutor
{
    private readonly IKubernetesDeployer _deployer;
    private readonly IBlueGreenDeployer _blueGreen;
    public KubernetesApplyStepExecutor(IKubernetesDeployer deployer, IBlueGreenDeployer blueGreen)
    {
        _deployer = deployer;
        _blueGreen = blueGreen;
    }

    public DeploymentStepKind Kind => DeploymentStepKind.KubernetesApply;

    public async Task<StepOutcome> ExecuteAsync(DeploymentContext ctx, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(ctx.KubernetesContext) || string.IsNullOrWhiteSpace(ctx.KubernetesNamespace))
            return StepOutcome.Fail("environment has no Kubernetes context/namespace.");
        if (ctx.Kubernetes is not { } spec)
            return StepOutcome.Fail("mapping has no Kubernetes spec.");

        var name = string.IsNullOrWhiteSpace(spec.DeploymentName) ? LeafName(ctx.ContainerName) : spec.DeploymentName;
        var port = spec.ContainerPort <= 0 ? 8080 : spec.ContainerPort;

        if (spec.Strategy == RolloutStrategy.BlueGreen)
            return await BlueGreenAsync(ctx, spec, name, port, ct).ConfigureAwait(false);

        var resource = await _deployer.ApplyAsync(new KubernetesApplyRequest(
            ctx.KubernetesContext!, ctx.KubernetesNamespace!, name, ctx.ImageToDeploy,
            port, spec.Replicas, spec.EnvVars, spec.ImagePullSecret, spec.CreateService), ct).ConfigureAwait(false);
        ctx.KubernetesResource = resource;
        return StepOutcome.Ok($"applied {resource}");
    }

    private async Task<StepOutcome> BlueGreenAsync(DeploymentContext ctx, KubernetesSpec spec, string name, int port, CancellationToken ct)
    {
        var result = await _blueGreen.DeployGreenAsync(new BlueGreenDeployRequest(
            ctx.KubernetesContext!, ctx.KubernetesNamespace!, name, ctx.ImageToDeploy,
            port, spec.Replicas, spec.EnvVars, spec.ImagePullSecret), ct).ConfigureAwait(false);
        ctx.KubernetesResource = result.Detail;

        // Bootstrap: the first deploy created the Service pointing at this slot — it's already live.
        if (result.ActiveSlot == result.GreenSlot)
            return StepOutcome.Ok($"deployed {name} ({result.GreenSlot}, live)");

        if (!result.Healthy)
        {
            await _blueGreen.RollbackAsync(ctx.KubernetesContext!, ctx.KubernetesNamespace!, name, result.GreenSlot, ct).ConfigureAwait(false);
            return StepOutcome.Fail($"green slot '{result.GreenSlot}' did not become healthy — rolled back.", StepFailureKind.Timeout);
        }

        if (spec.PromotionMode == PromotionMode.Automatic)
        {
            var promoted = await _blueGreen.PromoteAsync(ctx.KubernetesContext!, ctx.KubernetesNamespace!, name, result.GreenSlot, result.ActiveSlot, ct).ConfigureAwait(false);
            ctx.KubernetesResource = promoted;
            return StepOutcome.Ok($"promoted {name} to '{result.GreenSlot}'");
        }

        // Manual promotion → park the run in AwaitingPromotion.
        return StepOutcome.PausedForPromotion(result.GreenSlot, result.ActiveSlot,
            $"green '{result.GreenSlot}' healthy; awaiting promotion (active '{result.ActiveSlot}').");
    }

    private static string LeafName(string containerName)
    {
        var n = containerName.Trim().Trim('/');
        var slash = n.LastIndexOf('/');
        return slash >= 0 ? n[(slash + 1)..] : n;
    }
}
