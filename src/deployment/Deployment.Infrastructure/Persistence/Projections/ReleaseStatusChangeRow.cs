using Deployment.Domain.Releases;

namespace Deployment.Infrastructure.Persistence.Projections;

/// <summary>
/// Audit row for a single <c>Release</c> status transition (decisions §9.2).
/// Append-only. Built by a Wolverine event handler subscribing to
/// <c>ReleaseStatusChanged</c>. Lives in Infrastructure because it's a
/// read-model concern, not a domain invariant.
/// </summary>
public sealed class ReleaseStatusChangeRow
{
    public Guid ChangeId { get; init; }
    public Guid ReleaseId { get; init; }
    public ReleaseStatus FromStatus { get; init; }
    public ReleaseStatus ToStatus { get; init; }
    public string? Reason { get; init; }
    public string ChangedByPrincipal { get; init; } = string.Empty;
    public DateTimeOffset ChangedAtUtc { get; init; }
}
