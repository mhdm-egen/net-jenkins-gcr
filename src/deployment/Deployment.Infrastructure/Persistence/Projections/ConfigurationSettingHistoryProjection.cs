using Deployment.Domain.Configuration.Events;
using Microsoft.EntityFrameworkCore;

namespace Deployment.Infrastructure.Persistence.Projections;

/// <summary>
/// Projects <see cref="ConfigurationSettingChanged"/> domain events into the
/// <c>ConfigurationSettingHistory</c> read-model table (decisions §4.2).
/// Discovered by Wolverine via the conventional <c>Handle</c> method name.
/// Eventually consistent: dispatched by the UnitOfWork after the source row
/// is persisted.
/// </summary>
public sealed class ConfigurationSettingHistoryProjection
{
    public async Task Handle(
        ConfigurationSettingChanged evt,
        Persistence.DeploymentDbContext db,
        CancellationToken cancellationToken)
    {
        db.Set<ConfigurationSettingHistoryRow>().Add(new ConfigurationSettingHistoryRow
        {
            HistoryId = Guid.NewGuid(),
            ConfigurationSettingId = evt.ConfigurationSettingId,
            ChangeKind = evt.ChangeKind,
            OldValue = evt.OldValue,
            OldSecretReference = evt.OldSecretReference,
            OldIsSecret = evt.OldIsSecret,
            OldValueType = evt.OldValueType,
            NewValue = evt.NewValue,
            NewSecretReference = evt.NewSecretReference,
            NewIsSecret = evt.NewIsSecret,
            NewValueType = evt.NewValueType,
            ChangedByPrincipal = evt.ChangedByPrincipal,
            ChangedAtUtc = evt.OccurredAtUtc,
        });
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
