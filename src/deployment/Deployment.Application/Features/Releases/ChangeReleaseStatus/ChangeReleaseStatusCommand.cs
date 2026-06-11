using Deployment.Contracts.Releases;

namespace Deployment.Application.Features.Releases.ChangeReleaseStatus;

/// <summary>
/// Transition a <c>Release.Status</c> with audit. Drives the
/// <c>ReleaseStatusChange</c> history projection (decisions §9.2). Reason is
/// required for transitions to <c>Quarantined</c>; the domain enforces this.
/// </summary>
public sealed record ChangeReleaseStatusCommand(
    Guid ReleaseId,
    ReleaseStatusDto NewStatus,
    string? Reason,
    string ChangedByPrincipal);
