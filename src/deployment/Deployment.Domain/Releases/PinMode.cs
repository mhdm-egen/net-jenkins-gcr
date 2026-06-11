namespace Deployment.Domain.Releases;

/// <summary>
/// How a <see cref="ReleaseComposition"/> entry chooses the service release.
/// See decisions §3 for the resolution ladder.
/// </summary>
public enum PinMode
{
    /// <summary>Exact version: <c>ServiceReleaseId</c> is bound at composition time.</summary>
    Pinned = 0,

    /// <summary>Resolved to the newest <c>Available</c> release at deploy time.</summary>
    Latest = 1,

    /// <summary>
    /// Resolved to the release currently running in the target environment;
    /// falls back to <see cref="Latest"/> if the service has never been deployed there.
    /// </summary>
    Current = 2,
}
