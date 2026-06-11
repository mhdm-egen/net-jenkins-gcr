namespace Deployment.Domain.Releases;

/// <summary>
/// Lifecycle status of a release in the catalog. Drives pin-resolution candidacy:
/// only <see cref="Available"/> releases are considered by <c>Latest</c> resolution
/// (see decisions §3). Transitions are auditable via the <c>ReleaseStatusChange</c>
/// projection (§9.2).
/// </summary>
public enum ReleaseStatus
{
    /// <summary>Eligible for deployment and as a <c>Latest</c> candidate.</summary>
    Available = 0,

    /// <summary>A newer release has been published; kept for history and rollback targets.</summary>
    Superseded = 1,

    /// <summary>
    /// Pulled from candidacy — typically due to a CVE or failed verification.
    /// Can later return to <see cref="Available"/> after remediation.
    /// </summary>
    Quarantined = 2,
}
