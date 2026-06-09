using Deployment.Domain.Common;

namespace Deployment.Domain.Environments;

/// <summary>
/// A no-deploy window for an <see cref="Environment"/> (decisions §7.4).
/// Recurrence is not modelled — generators expand "every Friday 4pm–Monday 8am"
/// into explicit rows ahead of time. Scope is the environment only in v1; per-unit
/// freezes are deferred to v2.
///
/// Enforcement is soft: <c>StartDeployment</c> requires <c>OverrideFreezeReason</c>
/// when <c>StartedAtUtc</c> falls inside any active window for the target environment.
/// </summary>
public sealed class EnvironmentFreezeWindow : Entity<Guid>
{
    public Guid EnvironmentId { get; private set; }
    public DateTimeOffset StartUtc { get; private set; }
    public DateTimeOffset EndUtc { get; private set; }
    public string Reason { get; private set; }
    public string CreatedByPrincipal { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }

    private EnvironmentFreezeWindow()
    {
        Reason = string.Empty;
        CreatedByPrincipal = string.Empty;
    }

    internal EnvironmentFreezeWindow(
        Guid id,
        Guid environmentId,
        DateTimeOffset startUtc,
        DateTimeOffset endUtc,
        string reason,
        string createdByPrincipal,
        DateTimeOffset createdAtUtc)
    {
        if (id == Guid.Empty) throw new ArgumentException("Id cannot be empty.", nameof(id));
        if (environmentId == Guid.Empty)
            throw new ArgumentException("EnvironmentId cannot be empty.", nameof(environmentId));
        if (endUtc <= startUtc)
            throw new InvalidOperationException("FreezeWindow.EndUtc must be after StartUtc.");
        if (string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException("Reason cannot be empty.", nameof(reason));
        if (string.IsNullOrWhiteSpace(createdByPrincipal))
            throw new ArgumentException("CreatedByPrincipal cannot be empty.", nameof(createdByPrincipal));

        Id = id;
        EnvironmentId = environmentId;
        StartUtc = startUtc;
        EndUtc = endUtc;
        Reason = reason.Trim();
        CreatedByPrincipal = createdByPrincipal.Trim();
        CreatedAtUtc = createdAtUtc;
    }

    /// <summary>True if the given instant is within [StartUtc, EndUtc).</summary>
    public bool Contains(DateTimeOffset instant) => instant >= StartUtc && instant < EndUtc;
}
