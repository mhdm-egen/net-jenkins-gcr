namespace Jenkins.Domain.Handoffs;

/// <summary>
/// Lifecycle of a container→deployment handoff. <see cref="Pending"/> until the
/// deployment service accepts the release; <see cref="Skipped"/> when an operator
/// declines to promote a build.
/// </summary>
public enum HandoffStatus
{
    Pending = 0,
    Published = 1,
    Failed = 2,
    Skipped = 3,
}
