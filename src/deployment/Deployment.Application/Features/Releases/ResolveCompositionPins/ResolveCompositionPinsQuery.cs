namespace Deployment.Application.Features.Releases.ResolveCompositionPins;

/// <summary>
/// Resolve every BOM entry of an Application release into a concrete
/// service-release pin, in the context of a specific environment
/// (decisions §3). Pure query — does not mutate state.
/// </summary>
public sealed record ResolveCompositionPinsQuery(
    Guid ApplicationReleaseId,
    Guid EnvironmentId);

public sealed record ResolvedComposition(
    IReadOnlyList<ResolvedCompositionEntry> Entries);

public sealed record ResolvedCompositionEntry(
    Guid ServiceId,
    Guid ResolvedServiceReleaseId,
    PinResolutionReason Reason);

public enum PinResolutionReason
{
    /// <summary>The BOM entry was a hard <c>Pinned</c> version.</summary>
    Pinned = 0,

    /// <summary>Resolved to the newest <c>Available</c> release in the catalog.</summary>
    Latest = 1,

    /// <summary>Resolved to the release currently running in the target environment.</summary>
    Current = 2,

    /// <summary>
    /// <c>Current</c> was requested but the service had never been deployed
    /// successfully in the environment — fell back to <see cref="Latest"/>.
    /// Audit-flagged via a <c>CurrentPinFallbackApplied</c> deployment event.
    /// </summary>
    CurrentFellBackToLatest = 3,
}
