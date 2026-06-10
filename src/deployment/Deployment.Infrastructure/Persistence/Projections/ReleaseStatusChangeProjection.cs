using Deployment.Domain.Releases.Events;
using Microsoft.EntityFrameworkCore;

namespace Deployment.Infrastructure.Persistence.Projections;

/// <summary>
/// Projects <see cref="ReleaseStatusChanged"/> domain events into the
/// <c>ReleaseStatusChange</c> read-model table (decisions §9.2).
/// </summary>
public sealed class ReleaseStatusChangeProjection
{
    public async Task Handle(
        ReleaseStatusChanged evt,
        Persistence.DeploymentDbContext db,
        CancellationToken cancellationToken)
    {
        db.Set<ReleaseStatusChangeRow>().Add(new ReleaseStatusChangeRow
        {
            ChangeId = Guid.NewGuid(),
            ReleaseId = evt.ReleaseId,
            FromStatus = evt.FromStatus,
            ToStatus = evt.ToStatus,
            Reason = evt.Reason,
            ChangedByPrincipal = evt.ChangedByPrincipal,
            ChangedAtUtc = evt.OccurredAtUtc,
        });
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
