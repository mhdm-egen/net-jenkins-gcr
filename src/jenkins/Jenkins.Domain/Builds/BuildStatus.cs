namespace Jenkins.Domain.Builds;

/// <summary>
/// Lifecycle of a CI build, mirroring Jenkins' result model. A build is observed
/// <see cref="Running"/> while in flight and lands on one terminal state.
/// </summary>
public enum BuildStatus
{
    Queued = 0,
    Running = 1,
    Succeeded = 2,
    Failed = 3,
    Aborted = 4,
}
