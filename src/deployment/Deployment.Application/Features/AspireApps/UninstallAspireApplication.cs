using Microsoft.Extensions.Logging;
using Deployment.Application.Abstractions;
using Deployment.Contracts.AspireApps;
using Deployment.Domain.Abstractions;
using Deployment.Domain.AspireApps;
using Deployment.Domain.Environments;

namespace Deployment.Application.Features.AspireApps;

public sealed record UninstallAspireApplicationCommand(Guid ApplicationId, string? TriggeredBy);
public sealed record UninstallAspireApplicationResult(bool Applied, string Outcome, IReadOnlyList<string> Namespaces);

/// <summary>
/// Works out which cluster namespaces belong to an Aspire app.
///
/// The app record stores no namespace — it is derived from the environment plus the rollout strategy,
/// and blue-green can legitimately own TWO at once. Both the uninstall and the delete handler resolve
/// through here so the rule cannot drift between them; the string formatting deliberately matches the
/// existing deploy/promote/rollback sites (<c>$"{ns}-{slot}"</c>).
/// </summary>
internal static class AspireNamespaceResolver
{
    public static IReadOnlyList<string> Resolve(
        AspireApplication app,
        DeploymentEnvironment env,
        IReadOnlyList<AspireApplicationRunDto> runs)
    {
        var baseNs = env.KubernetesNamespace;
        if (string.IsNullOrWhiteSpace(baseNs)) return Array.Empty<string>();

        var result = new List<string>(2);

        if (app.Strategy == Domain.Mappings.RolloutStrategy.BlueGreen)
        {
            // Nothing is live until the first deploy sets a slot, so a null ActiveSlot means the base
            // namespace was never used by this app and must NOT be deleted — for blue-green the base
            // namespace is only ever a prefix.
            if (!string.IsNullOrWhiteSpace(app.ActiveSlot))
                result.Add($"{baseNs}-{app.ActiveSlot}");

            // A run parked in AwaitingPromotion has a green namespace on the cluster that no slot
            // pointer references yet. Skipping it is how you strand a namespace.
            foreach (var run in runs)
            {
                if (run.Status != AspireRunStatusDto.AwaitingPromotion) continue;
                if (string.IsNullOrWhiteSpace(run.RolloutGreenSlot)) continue;
                result.Add($"{run.Namespace}-{run.RolloutGreenSlot}");
            }
        }
        else
        {
            result.Add(baseNs);
        }

        return result.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    }

    /// <summary>True while a deploy is in flight — tearing the namespace out from under it would leave
    /// the run writing into a namespace that is being deleted.</summary>
    public static bool HasRunInFlight(IReadOnlyList<AspireApplicationRunDto> runs) =>
        runs.Any(r => r.Status is AspireRunStatusDto.Pending or AspireRunStatusDto.Running);
}

/// <summary>
/// Removes an Aspire app's workloads from the cluster by deleting its namespace(s), keeping the
/// registration so it can be redeployed. Idempotent — a missing namespace or a never-deployed app
/// succeeds. Modelled on <c>TeardownPreviewEnvironmentHandler</c>.
/// </summary>
public sealed class UninstallAspireApplicationHandler
{
    private readonly IAspireApplicationRepository _apps;
    private readonly IEnvironmentRepository _envs;
    private readonly IAspireApplicationRunReader _runs;
    private readonly INamespaceManager _namespaces;
    private readonly IUnitOfWork _uow;
    private readonly TimeProvider _clock;
    private readonly ILogger<UninstallAspireApplicationHandler> _logger;

    public UninstallAspireApplicationHandler(
        IAspireApplicationRepository apps, IEnvironmentRepository envs, IAspireApplicationRunReader runs,
        INamespaceManager namespaces, IUnitOfWork uow, TimeProvider clock,
        ILogger<UninstallAspireApplicationHandler> logger)
    {
        _apps = apps; _envs = envs; _runs = runs; _namespaces = namespaces;
        _uow = uow; _clock = clock; _logger = logger;
    }

    public async Task<UninstallAspireApplicationResult> HandleAsync(UninstallAspireApplicationCommand cmd, CancellationToken ct = default)
    {
        var app = await _apps.GetByIdAsync(cmd.ApplicationId, ct).ConfigureAwait(false);
        if (app is null) return Nothing("app-not-found", applied: false);

        var env = await _envs.GetByIdAsync(app.EnvironmentId, ct).ConfigureAwait(false);
        if (env is null || string.IsNullOrWhiteSpace(env.KubernetesContext) || string.IsNullOrWhiteSpace(env.KubernetesNamespace))
            return Nothing("environment-not-kubernetes", applied: false);

        var runs = await _runs.ListAsync(app.Id, ct).ConfigureAwait(false);
        if (AspireNamespaceResolver.HasRunInFlight(runs)) return Nothing("deploy-in-progress", applied: false);

        var namespaces = AspireNamespaceResolver.Resolve(app, env, runs);
        if (namespaces.Count == 0)
        {
            // Never deployed: there is nothing on the cluster, which is the state the caller wanted.
            return Nothing("not-deployed", applied: true);
        }

        foreach (var ns in namespaces)
            await _namespaces.DeleteNamespaceAsync(env.KubernetesContext, ns, ct).ConfigureAwait(false);

        var now = _clock.GetUtcNow();

        // AutoDeploy is deliberately left alone. An uninstalled app stays down until CI produces a
        // genuinely NEWER successful build — the consumer's manifest/version check plus the ordering
        // guard (CiVersion.IsOlderThan) mean a redelivered or backfilled publish can't resurrect it.
        app.MarkUninstalled(namespaces, now);

        await _uow.SaveChangesAsync(ct).ConfigureAwait(false);
        _logger.LogInformation("[aspire] {App} uninstalled (namespaces {Namespaces} deleted on {Context}) by {By}.",
            app.Name, string.Join(", ", namespaces), env.KubernetesContext, cmd.TriggeredBy ?? "manual");

        return new UninstallAspireApplicationResult(true, "uninstalled", namespaces);
    }

    private static UninstallAspireApplicationResult Nothing(string outcome, bool applied) =>
        new(applied, outcome, Array.Empty<string>());
}
