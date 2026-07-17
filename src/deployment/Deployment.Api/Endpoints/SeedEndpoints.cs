using Deployment.Application.Features.AspireApps;
using Deployment.Application.Features.Environments;
using Deployment.Application.Features.Mappings;
using Deployment.Application.Features.Services;
using Deployment.Contracts.Mappings;
using Deployment.Contracts.Seed;
using Deployment.Domain.AspireApps;
using Deployment.Domain.Environments;
using Deployment.Domain.Mappings;
using Deployment.Domain.Services;

namespace Deployment.Api.Endpoints;

/// <summary>
/// Admin "demo setup" seed: installs curated demo <b>configuration</b> (environments, services,
/// mappings, Aspire apps) so an operator can then trigger real builds/deploys during a demo. The
/// inverse of the reset danger-zone — additive and idempotent (find-by-name skip), never destructive.
/// The names/targets mirror the real bundled demo topology (sample-aspire → local-k8s, Web App →
/// Cloud Run 'dev', bgweb/cweb blue-green, apiservice-k8s), so on an unmodified system every item is
/// skipped, and after a reset it restores exactly that setup. Reuses the existing Create* handlers so
/// validation + domain invariants are enforced; creating config raises only config events (no
/// consumers), so seeding kicks off no deploy.
/// </summary>
public static class SeedEndpoints
{
    public static IEndpointRouteBuilder MapSeedEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/deployment/seed-demo", async (SeedDemoRequest body, SeedDemoHandler h, CancellationToken ct) =>
            Results.Ok(await h.HandleAsync(body, ct))).WithTags("Seed");
        return app;
    }
}

public sealed class SeedDemoHandler
{
    // Grounded demo constants — mirror the real registered demo topology (docker-desktop cluster + the
    // 'dev' GCP Cloud Run target). Kept in sync with the bundled samples + docs/demos.
    private const string KubeContext = "docker-desktop";
    private const string PullSecret = "nexus-pull";

    // Scenario 1 — Aspire → auto-deploy (sample-aspire publishes APP_NAME=sampleapp → local-k8s).
    private const string K8sEnvName = "local-k8s";
    private const string K8sNamespace = "sampleapp";
    private const string AspireAppName = "Sample App";
    private const string AspireSourceKey = "sampleapp";
    private const string AspireManifestSource = "http://nexus:8081/repository/raw-hosted/sampleapp/1.0.0/aspirate-output.tar.gz";

    // Scenario 2 — Blue-green / canary K8s.
    private const string BgEnvName = "bg-test";
    private const string BgNamespace = "bgtest";
    private const string BgServiceName = "bgweb";
    private const string BgContainer = "bgweb";
    private const string CanaryEnvName = "canary-test";
    private const string CanaryNamespace = "canarytest";
    private const string CanaryServiceName = "cweb";
    private const string CanaryContainer = "cweb";

    // Scenario 3 — Container / Cloud Run (Web App publishes 'webapphost' → GAR/Cloud Run in 'dev').
    private const string DevEnvName = "dev";
    private const string DevGcpProject = "egen-gcr";
    private const string DevRegion = "us-west1";
    private const string DevGarRepository = "egen-cicd-net";
    private const string CloudRunServiceName = "web-app-host";
    private const string CloudRunContainer = "webapphost";

    // Scenario 4 — k8s admin (a plain Direct k8s service on the local cluster).
    private const string ApiServiceName = "apiservice-k8s";
    private const string ApiContainer = "apiservice";

    private readonly CreateServiceHandler _createService;
    private readonly CreateEnvironmentHandler _createEnv;
    private readonly CreateMappingHandler _createMapping;
    private readonly CreateAspireApplicationHandler _createAspire;
    private readonly SetAspireAutoDeployHandler _setAspireAutoDeploy;
    private readonly IServiceRepository _services;
    private readonly IEnvironmentRepository _envs;
    private readonly IDeploymentMappingRepository _mappings;
    private readonly IAspireApplicationRepository _apps;
    private readonly ILogger<SeedDemoHandler> _logger;

    public SeedDemoHandler(
        CreateServiceHandler createService, CreateEnvironmentHandler createEnv, CreateMappingHandler createMapping,
        CreateAspireApplicationHandler createAspire, SetAspireAutoDeployHandler setAspireAutoDeploy,
        IServiceRepository services, IEnvironmentRepository envs, IDeploymentMappingRepository mappings,
        IAspireApplicationRepository apps, ILogger<SeedDemoHandler> logger)
    {
        _createService = createService;
        _createEnv = createEnv;
        _createMapping = createMapping;
        _createAspire = createAspire;
        _setAspireAutoDeploy = setAspireAutoDeploy;
        _services = services;
        _envs = envs;
        _mappings = mappings;
        _apps = apps;
        _logger = logger;
    }

    public async Task<SeedDemoResultDto> HandleAsync(SeedDemoRequest req, CancellationToken ct)
    {
        var items = new List<SeedItemDto>();
        var log = new List<string>();
        void Log(string m) => log.Add(m);
        void Record(string kind, string name, bool created)
        {
            items.Add(new SeedItemDto(kind, name, created ? "created" : "skipped"));
            Log($"{(created ? "created" : "skipped")} {kind}: {name}");
        }

        // The local k8s env is shared by the Aspire + k8s-admin scenarios — create it once.
        Guid localK8sEnvId = default;
        if (req.AspireAutoDeploy || req.K8sAdmin)
            localK8sEnvId = await EnsureK8sEnvAsync(K8sEnvName, K8sNamespace, Record, ct);

        // Scenario 1 — Aspire → auto-deploy.
        if (req.AspireAutoDeploy)
        {
            var existing = await _apps.FindByNameAsync(AspireAppName, ct);
            Guid appId;
            if (existing is not null) { appId = existing.Id; Record("aspire-app", AspireAppName, created: false); }
            else
            {
                var dto = await _createAspire.HandleAsync(new CreateAspireApplicationCommand(
                    AspireAppName, "Bundled Aspire sample (sample-aspire).", localK8sEnvId,
                    AspireManifestSource, Version: null, SourceKey: AspireSourceKey, MainBranch: "main"), ct);
                appId = dto.Id; Record("aspire-app", AspireAppName, created: true);
            }
            // Ensure the demo invariant (auto-deploy on) even when the app pre-existed — idempotent.
            await _setAspireAutoDeploy.HandleAsync(new SetAspireAutoDeployCommand(appId, true), ct);
            Log($"aspire-app auto-deploy: on ({AspireAppName})");
        }

        // Scenario 2 — Blue-green / canary K8s.
        if (req.BlueGreenK8s)
        {
            var bgEnvId = await EnsureK8sEnvAsync(BgEnvName, BgNamespace, Record, ct);
            var bgSvcId = await EnsureServiceAsync(BgServiceName, BgContainer, Record, ct);
            await EnsureMappingAsync(bgSvcId, bgEnvId, cloudRunServiceName: null,
                k8s: new KubernetesSpecDto(BgContainer, 80, 1, EnvVars: null, PullSecret, CreateService: true,
                    Strategy: RolloutStrategyDto.BlueGreen, PromotionMode: PromotionModeDto.Automatic),
                autoDeploy: false, kind: "blue-green mapping", displayName: $"{BgServiceName} → {BgEnvName}", Record, ct);

            var canEnvId = await EnsureK8sEnvAsync(CanaryEnvName, CanaryNamespace, Record, ct);
            var canSvcId = await EnsureServiceAsync(CanaryServiceName, CanaryContainer, Record, ct);
            await EnsureMappingAsync(canSvcId, canEnvId, cloudRunServiceName: null,
                k8s: new KubernetesSpecDto(CanaryContainer, 80, 4, EnvVars: null, PullSecret, CreateService: true,
                    Strategy: RolloutStrategyDto.Canary, PromotionMode: PromotionModeDto.Manual),
                autoDeploy: false, kind: "canary mapping", displayName: $"{CanaryServiceName} → {CanaryEnvName}", Record, ct);
        }

        // Scenario 3 — Container / Cloud Run (real 'dev' GAR + Cloud Run target).
        Guid? cloudRunServiceId = null;
        if (req.CloudRun)
        {
            var devEnvId = await EnsureCloudRunEnvAsync(DevEnvName, DevGcpProject, DevRegion, DevGarRepository, Record, ct);
            var crSvcId = await EnsureServiceAsync(CloudRunServiceName, CloudRunContainer, Record, ct);
            await EnsureMappingAsync(crSvcId, devEnvId, cloudRunServiceName: CloudRunContainer, k8s: null,
                autoDeploy: true, kind: "cloud-run mapping", displayName: $"{CloudRunServiceName} → {DevEnvName}", Record, ct);
            cloudRunServiceId = crSvcId;
        }

        // Scenario 4 — k8s admin: a plain Direct k8s service on the local cluster. The K8s screens
        // themselves read the live cluster (kubeconfig + context) and need no registered environment.
        if (req.K8sAdmin)
        {
            var apiSvcId = await EnsureServiceAsync(ApiServiceName, ApiContainer, Record, ct);
            await EnsureMappingAsync(apiSvcId, localK8sEnvId, cloudRunServiceName: null,
                k8s: new KubernetesSpecDto(ApiContainer, 8080, 1, EnvVars: null, PullSecret, CreateService: true,
                    Strategy: RolloutStrategyDto.Direct, PromotionMode: PromotionModeDto.Automatic),
                autoDeploy: false, kind: "k8s mapping", displayName: $"{ApiServiceName} → {K8sEnvName}", Record, ct);
            Log($"k8s admin: reads the live cluster ({KubeContext}); ensured env '{K8sEnvName}' + service '{ApiServiceName}'.");
        }

        var created = items.Count(i => i.Status == "created");
        var skipped = items.Count(i => i.Status == "skipped");
        _logger.LogInformation("[seed] demo config seed — created={Created} skipped={Skipped}", created, skipped);
        return new SeedDemoResultDto(created, skipped, items, log,
            cloudRunServiceId, cloudRunServiceId is null ? null : CloudRunServiceName,
            cloudRunServiceId is null ? null : CloudRunContainer);
    }

    private async Task<Guid> EnsureK8sEnvAsync(string name, string ns, Action<string, string, bool> record, CancellationToken ct)
    {
        var existing = await _envs.FindByNameAsync(name, ct);
        if (existing is not null) { record("environment", name, false); return existing.Id; }
        var dto = await _createEnv.HandleAsync(new CreateEnvironmentCommand(
            name, GcpProject: null, Region: null, GarRepository: null,
            KubernetesContext: KubeContext, KubernetesNamespace: ns), ct);
        record("environment", name, true);
        return dto.Id;
    }

    private async Task<Guid> EnsureCloudRunEnvAsync(string name, string project, string region, string gar, Action<string, string, bool> record, CancellationToken ct)
    {
        var existing = await _envs.FindByNameAsync(name, ct);
        if (existing is not null) { record("environment", name, false); return existing.Id; }
        var dto = await _createEnv.HandleAsync(new CreateEnvironmentCommand(
            name, GcpProject: project, Region: region, GarRepository: gar,
            KubernetesContext: null, KubernetesNamespace: null), ct);
        record("environment", name, true);
        return dto.Id;
    }

    private async Task<Guid> EnsureServiceAsync(string name, string container, Action<string, string, bool> record, CancellationToken ct)
    {
        var existing = await _services.FindByNameAsync(name, ct);
        if (existing is not null) { record("service", name, false); return existing.Id; }
        var dto = await _createService.HandleAsync(new CreateServiceCommand(name, container), ct);
        record("service", name, true);
        return dto.Id;
    }

    private async Task EnsureMappingAsync(
        Guid serviceId, Guid environmentId, string? cloudRunServiceName, KubernetesSpecDto? k8s,
        bool autoDeploy, string kind, string displayName, Action<string, string, bool> record, CancellationToken ct)
    {
        if (await _mappings.FindAsync(serviceId, environmentId, ct) is not null) { record(kind, displayName, false); return; }
        await _createMapping.HandleAsync(new CreateMappingCommand(serviceId, environmentId, cloudRunServiceName, k8s, autoDeploy, Steps: null), ct);
        record(kind, displayName, true);
    }
}
