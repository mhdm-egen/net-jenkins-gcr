using Deployment.Application.Features.Configuration.ListConfigurationSettings;
using Deployment.Contracts.Configuration;
using Microsoft.EntityFrameworkCore;

namespace Deployment.Infrastructure.Persistence.Readers;

internal sealed class EfConfigurationCatalogReader : IConfigurationCatalogReader
{
    private readonly DeploymentDbContext _db;

    public EfConfigurationCatalogReader(DeploymentDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<ConfigurationSettingDto>> ListByUnitAsync(
        Guid deployableUnitId, CancellationToken cancellationToken = default)
    {
        // Pull the settings + env-name lookup separately so the join stays
        // index-friendly (the unique idx on Settings is unit + env + key).
        var rows = await _db.ConfigurationSettings.AsNoTracking()
            .Where(s => s.DeployableUnitId == deployableUnitId)
            .OrderBy(s => s.Key)
            .Select(s => new
            {
                s.Id, s.DeployableUnitId, s.EnvironmentId, s.Key, s.Value,
                s.IsSecret, s.SecretReference, s.ValueType,
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if (rows.Count == 0) return Array.Empty<ConfigurationSettingDto>();

        var envIds = rows
            .Where(r => r.EnvironmentId.HasValue)
            .Select(r => r.EnvironmentId!.Value)
            .Distinct()
            .ToList();

        var envNames = await _db.Environments.AsNoTracking()
            .Where(e => envIds.Contains(e.Id))
            .ToDictionaryAsync(e => e.Id, e => e.Name, cancellationToken)
            .ConfigureAwait(false);

        return rows.Select(r => new ConfigurationSettingDto(
            Id: r.Id,
            DeployableUnitId: r.DeployableUnitId,
            EnvironmentId: r.EnvironmentId,
            EnvironmentName: r.EnvironmentId is { } eid && envNames.TryGetValue(eid, out var n) ? n : null,
            Key: r.Key,
            Value: r.Value,
            IsSecret: r.IsSecret,
            SecretReference: r.SecretReference,
            ValueType: (ConfigurationValueTypeDto)(int)r.ValueType))
            .ToList();
    }

    public async Task<ConfigurationSettingDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var row = await _db.ConfigurationSettings.AsNoTracking()
            .Where(s => s.Id == id)
            .Select(s => new
            {
                s.Id, s.DeployableUnitId, s.EnvironmentId, s.Key, s.Value,
                s.IsSecret, s.SecretReference, s.ValueType,
            })
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        if (row is null) return null;

        string? envName = null;
        if (row.EnvironmentId is { } eid)
        {
            envName = await _db.Environments.AsNoTracking()
                .Where(e => e.Id == eid)
                .Select(e => e.Name)
                .FirstOrDefaultAsync(cancellationToken)
                .ConfigureAwait(false);
        }

        return new ConfigurationSettingDto(
            Id: row.Id,
            DeployableUnitId: row.DeployableUnitId,
            EnvironmentId: row.EnvironmentId,
            EnvironmentName: envName,
            Key: row.Key,
            Value: row.Value,
            IsSecret: row.IsSecret,
            SecretReference: row.SecretReference,
            ValueType: (ConfigurationValueTypeDto)(int)row.ValueType);
    }

    public async Task<IReadOnlyList<ConfigurationSettingHistoryDto>> GetHistoryAsync(
        Guid settingId, CancellationToken cancellationToken = default)
    {
        return await _db.ConfigurationSettingHistory.AsNoTracking()
            .Where(h => h.ConfigurationSettingId == settingId)
            .OrderByDescending(h => h.ChangedAtUtc)
            .Select(h => new ConfigurationSettingHistoryDto(
                h.HistoryId,
                h.ConfigurationSettingId,
                (ConfigurationChangeKindDto)(int)h.ChangeKind,
                h.OldValue,
                h.OldSecretReference,
                h.OldIsSecret,
                h.OldValueType.HasValue ? (ConfigurationValueTypeDto)(int)h.OldValueType.Value : null,
                h.NewValue,
                h.NewSecretReference,
                h.NewIsSecret,
                h.NewValueType.HasValue ? (ConfigurationValueTypeDto)(int)h.NewValueType.Value : null,
                h.ChangedByPrincipal,
                h.ChangedAtUtc))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }
}
