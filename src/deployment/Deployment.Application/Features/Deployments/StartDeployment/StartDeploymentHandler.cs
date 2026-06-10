using System.Text.Json;
using Deployment.Application.Abstractions;
using Deployment.Application.Features.Configuration.ResolveEffectiveConfig;
using Deployment.Application.Features.Releases.ResolveCompositionPins;
using Deployment.Domain.Abstractions;
using Deployment.Domain.Deployments;
using Deployment.Domain.DeployableUnits;
using Deployment.Domain.Environments;
using Deployment.Domain.Releases;
using Environment = Deployment.Domain.Environments.Environment;
using DeploymentRow = Deployment.Domain.Deployments.Deployment;

namespace Deployment.Application.Features.Deployments.StartDeployment;

/// <summary>
/// Orchestrates the full start-of-deployment workflow. Single Unit of Work:
/// promotion-path check → freeze-window check → pin resolution → row creation
/// (parent + leaves per the cascade convention) → secret-binding snapshot per
/// leaf row. Status starts at <c>Queued</c>; the deployment runner advances
/// rows from there.
/// </summary>
public sealed class StartDeploymentHandler
{
    private readonly IReleaseRepository _releases;
    private readonly IEnvironmentRepository _environments;
    private readonly IApplicationRepository _applications;
    private readonly IDeploymentRepository _deployments;
    private readonly ResolveCompositionPinsHandler _resolvePins;
    private readonly ResolveEffectiveConfigHandler _resolveConfig;
    private readonly ISecretResolver _secretResolver;
    private readonly IUnitOfWork _uow;
    private readonly TimeProvider _clock;

    public StartDeploymentHandler(
        IReleaseRepository releases,
        IEnvironmentRepository environments,
        IApplicationRepository applications,
        IDeploymentRepository deployments,
        ResolveCompositionPinsHandler resolvePins,
        ResolveEffectiveConfigHandler resolveConfig,
        ISecretResolver secretResolver,
        IUnitOfWork uow,
        TimeProvider clock)
    {
        _releases = releases;
        _environments = environments;
        _applications = applications;
        _deployments = deployments;
        _resolvePins = resolvePins;
        _resolveConfig = resolveConfig;
        _secretResolver = secretResolver;
        _uow = uow;
        _clock = clock;
    }

    public async Task<StartedDeployment> HandleAsync(
        StartDeploymentCommand cmd,
        CancellationToken cancellationToken = default)
    {
        var now = _clock.GetUtcNow();

        var release = await _releases.GetByIdAsync(cmd.ReleaseId, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Release {cmd.ReleaseId} not found.");
        if (release.Status != ReleaseStatus.Available)
            throw new InvalidOperationException(
                $"Release {release.Id} is {release.Status}; only Available releases can be deployed.");

        var environment = await _environments.GetByIdAsync(cmd.EnvironmentId, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Environment {cmd.EnvironmentId} not found.");

        await EnforcePromotionPathAsync(release, environment, cmd.SkipPromotionPathReason, cancellationToken)
            .ConfigureAwait(false);
        EnforceFreezeWindow(environment, now, cmd.OverrideFreezeReason);

        var targets = ResolveTargets(environment, cmd.TargetIds);

        StartedDeployment result = release.ArtifactType == ArtifactType.Manifest
            ? await StartApplicationCascadeAsync(release, environment, targets, cmd, now, cancellationToken).ConfigureAwait(false)
            : await StartServiceLeavesAsync(release, environment, targets, cmd, now, cancellationToken).ConfigureAwait(false);

        await _uow.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return result;
    }

    private async Task EnforcePromotionPathAsync(
        Release release,
        Environment environment,
        string? skipReason,
        CancellationToken cancellationToken)
    {
        if (environment.PromotionRank == 0) return; // nothing below the bottom rank

        var lowerRank = environment.PromotionRank - 1;
        var lower = await _environments.FindByPromotionRankAsync(lowerRank, cancellationToken).ConfigureAwait(false);
        if (lower is null) return; // no defined predecessor — skip the check

        var prior = await _deployments.FindLatestSucceededAsync(release.DeployableUnitId, lower.Id, cancellationToken)
            .ConfigureAwait(false);

        var sameReleaseSucceeded = prior is not null && prior.ReleaseId == release.Id;
        if (sameReleaseSucceeded) return;

        if (string.IsNullOrWhiteSpace(skipReason))
            throw new InvalidOperationException(
                $"Promotion-path policy: release {release.Id} has not succeeded in environment '{lower.Name}' (rank {lowerRank}). " +
                "Set SkipPromotionPathReason to deploy out-of-order with an audit reason.");
    }

    private static void EnforceFreezeWindow(Environment environment, DateTimeOffset now, string? overrideReason)
    {
        if (!environment.IsFrozenAt(now)) return;
        if (string.IsNullOrWhiteSpace(overrideReason))
            throw new InvalidOperationException(
                $"Environment '{environment.Name}' is in a freeze window at {now:o}. " +
                "Set OverrideFreezeReason to deploy with an audit reason.");
    }

    private static IReadOnlyList<DeploymentTarget> ResolveTargets(
        Environment environment,
        IReadOnlyList<Guid> targetIds)
    {
        if (environment.Targets.Count == 0)
            throw new InvalidOperationException(
                $"Environment '{environment.Name}' has no DeploymentTargets configured.");

        if (targetIds.Count == 0) return environment.Targets.ToList();

        var byId = environment.Targets.ToDictionary(t => t.Id);
        var picked = new List<DeploymentTarget>(targetIds.Count);
        foreach (var id in targetIds)
        {
            if (!byId.TryGetValue(id, out var target))
                throw new InvalidOperationException(
                    $"Target {id} does not belong to environment '{environment.Name}'.");
            picked.Add(target);
        }
        return picked;
    }

    private async Task<StartedDeployment> StartApplicationCascadeAsync(
        Release appRelease,
        Environment environment,
        IReadOnlyList<DeploymentTarget> targets,
        StartDeploymentCommand cmd,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var resolved = await _resolvePins.HandleAsync(
            new ResolveCompositionPinsQuery(appRelease.Id, environment.Id), cancellationToken)
            .ConfigureAwait(false);

        var parent = new DeploymentRow(
            id: Guid.NewGuid(),
            releaseId: appRelease.Id,
            environmentId: environment.Id,
            targetId: null,
            parentDeploymentId: null,
            strategy: cmd.Strategy.ToDomain(),
            trigger: cmd.Trigger.ToDomain(),
            triggeredByPrincipal: cmd.TriggeredByPrincipal,
            skipPromotionPathReason: cmd.SkipPromotionPathReason,
            overrideFreezeReason: cmd.OverrideFreezeReason,
            queuedAtUtc: now);

        // Record fallback audit on the parent so the resolver decision is preserved.
        foreach (var fb in resolved.Entries.Where(e => e.Reason == PinResolutionReason.CurrentFellBackToLatest))
        {
            parent.RecordAuditEvent(
                Guid.NewGuid(),
                "CurrentPinFallbackApplied",
                JsonSerializer.Serialize(new
                {
                    fb.ServiceId,
                    fb.ResolvedServiceReleaseId,
                    Reason = "NeverDeployedInEnvironment",
                }),
                now);
        }

        await _deployments.AddAsync(parent, cancellationToken).ConfigureAwait(false);

        var appUnitId = appRelease.DeployableUnitId;
        var children = new List<Guid>(resolved.Entries.Count * targets.Count);

        foreach (var entry in resolved.Entries)
        {
            foreach (var target in targets)
            {
                var child = new DeploymentRow(
                    id: Guid.NewGuid(),
                    releaseId: entry.ResolvedServiceReleaseId,
                    environmentId: environment.Id,
                    targetId: target.Id,
                    parentDeploymentId: parent.Id,
                    strategy: cmd.Strategy.ToDomain(),
                    trigger: cmd.Trigger.ToDomain(),
                    triggeredByPrincipal: cmd.TriggeredByPrincipal,
                    skipPromotionPathReason: null,
                    overrideFreezeReason: null,
                    queuedAtUtc: now);

                await AttachSecretBindingsAsync(child, entry.ServiceId, appUnitId, environment.Id, now, cancellationToken)
                    .ConfigureAwait(false);

                await _deployments.AddAsync(child, cancellationToken).ConfigureAwait(false);
                children.Add(child.Id);
            }
        }

        return new StartedDeployment(parent.Id, children);
    }

    private async Task<StartedDeployment> StartServiceLeavesAsync(
        Release svcRelease,
        Environment environment,
        IReadOnlyList<DeploymentTarget> targets,
        StartDeploymentCommand cmd,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var children = new List<Guid>(targets.Count);
        foreach (var target in targets)
        {
            var leaf = new DeploymentRow(
                id: Guid.NewGuid(),
                releaseId: svcRelease.Id,
                environmentId: environment.Id,
                targetId: target.Id,
                parentDeploymentId: null,
                strategy: cmd.Strategy.ToDomain(),
                trigger: cmd.Trigger.ToDomain(),
                triggeredByPrincipal: cmd.TriggeredByPrincipal,
                skipPromotionPathReason: cmd.SkipPromotionPathReason,
                overrideFreezeReason: cmd.OverrideFreezeReason,
                queuedAtUtc: now);

            // No enclosing application context for a standalone service deploy.
            await AttachSecretBindingsAsync(leaf, svcRelease.DeployableUnitId, applicationId: null,
                environmentId: environment.Id, now, cancellationToken)
                .ConfigureAwait(false);

            await _deployments.AddAsync(leaf, cancellationToken).ConfigureAwait(false);
            children.Add(leaf.Id);
        }

        return new StartedDeployment(ParentDeploymentId: null, children);
    }

    private async Task AttachSecretBindingsAsync(
        DeploymentRow row,
        Guid serviceId,
        Guid? applicationId,
        Guid environmentId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var effective = await _resolveConfig.HandleAsync(
            new ResolveEffectiveConfigQuery(serviceId, applicationId, environmentId),
            cancellationToken).ConfigureAwait(false);

        foreach (var entry in effective.Entries)
        {
            if (!entry.IsSecret || string.IsNullOrWhiteSpace(entry.SecretReference)) continue;

            var versionedUri = await _secretResolver.ResolveCurrentVersionAsync(entry.SecretReference, cancellationToken)
                .ConfigureAwait(false);
            row.AddSecretBinding(entry.ConfigurationSettingId, versionedUri, now);
        }
    }
}
