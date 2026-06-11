using Deployment.Domain.Deployments;
using Deployment.Domain.Releases;

namespace Deployment.Application.Features.Releases.ResolveCompositionPins;

/// <summary>
/// Resolution ladder per decisions §3:
/// <list type="number">
///   <item><c>Pinned</c> → use <c>ServiceReleaseId</c> directly.</item>
///   <item><c>Latest</c> → newest <c>Available</c> release for the service.</item>
///   <item><c>Current</c> → most recent succeeded deployment of the service in the
///         target environment; falls back to <c>Latest</c> if none.</item>
/// </list>
/// Hard-fails if any required entry has no candidate at all
/// ("service has no available releases").
/// </summary>
public sealed class ResolveCompositionPinsHandler
{
    private readonly IReleaseRepository _releases;
    private readonly IDeploymentRepository _deployments;

    public ResolveCompositionPinsHandler(
        IReleaseRepository releases,
        IDeploymentRepository deployments)
    {
        _releases = releases;
        _deployments = deployments;
    }

    public async Task<ResolvedComposition> HandleAsync(
        ResolveCompositionPinsQuery query,
        CancellationToken cancellationToken = default)
    {
        var appRelease = await _releases.GetByIdAsync(query.ApplicationReleaseId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new InvalidOperationException(
                $"Application release {query.ApplicationReleaseId} not found.");

        if (appRelease.ArtifactType != ArtifactType.Manifest)
            throw new InvalidOperationException(
                $"Release {appRelease.Id} is not an Application release (ArtifactType={appRelease.ArtifactType}); has no compositions to resolve.");

        var resolved = new List<ResolvedCompositionEntry>(appRelease.Compositions.Count);

        foreach (var entry in appRelease.Compositions)
        {
            var (releaseId, reason) = await ResolveOneAsync(entry, query.EnvironmentId, cancellationToken)
                .ConfigureAwait(false);
            resolved.Add(new ResolvedCompositionEntry(entry.ServiceId, releaseId, reason));
        }

        return new ResolvedComposition(resolved);
    }

    private async Task<(Guid releaseId, PinResolutionReason reason)> ResolveOneAsync(
        ReleaseComposition entry,
        Guid environmentId,
        CancellationToken cancellationToken)
    {
        switch (entry.PinMode)
        {
            case PinMode.Pinned:
                return (entry.ServiceReleaseId!.Value, PinResolutionReason.Pinned);

            case PinMode.Latest:
                {
                    var latest = await _releases.FindLatestAvailableAsync(entry.ServiceId, cancellationToken)
                        .ConfigureAwait(false)
                        ?? throw new InvalidOperationException(
                            $"Service {entry.ServiceId} has no available releases — cannot resolve Latest pin.");
                    return (latest.Id, PinResolutionReason.Latest);
                }

            case PinMode.Current:
                {
                    var current = await _deployments.FindLatestSucceededAsync(entry.ServiceId, environmentId, cancellationToken)
                        .ConfigureAwait(false);
                    if (current is not null)
                        return (current.ReleaseId, PinResolutionReason.Current);

                    // Fall back to Latest. The caller is expected to record a
                    // CurrentPinFallbackApplied DeploymentEvent (decisions §3).
                    var latest = await _releases.FindLatestAvailableAsync(entry.ServiceId, cancellationToken)
                        .ConfigureAwait(false)
                        ?? throw new InvalidOperationException(
                            $"Service {entry.ServiceId} has neither a current deployment in environment {environmentId} nor any available release — cannot resolve Current pin.");
                    return (latest.Id, PinResolutionReason.CurrentFellBackToLatest);
                }

            default:
                throw new InvalidOperationException($"Unknown PinMode {entry.PinMode}.");
        }
    }
}
