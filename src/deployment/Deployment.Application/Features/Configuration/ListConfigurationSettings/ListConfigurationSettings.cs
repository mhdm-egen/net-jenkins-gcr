using Deployment.Contracts.Configuration;

namespace Deployment.Application.Features.Configuration.ListConfigurationSettings;

public interface IConfigurationCatalogReader
{
    /// <summary>
    /// All settings owned by a deployable unit (every env + unit-defaults).
    /// </summary>
    Task<IReadOnlyList<ConfigurationSettingDto>> ListByUnitAsync(
        Guid deployableUnitId,
        CancellationToken cancellationToken = default);

    Task<ConfigurationSettingDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Full change timeline for one setting, newest first. Reads the
    /// <c>ConfigurationSettingHistory</c> projection.
    /// </summary>
    Task<IReadOnlyList<ConfigurationSettingHistoryDto>> GetHistoryAsync(
        Guid settingId,
        CancellationToken cancellationToken = default);
}

public sealed record ListConfigurationSettingsByUnitQuery(Guid DeployableUnitId);

public sealed class ListConfigurationSettingsByUnitHandler
{
    private readonly IConfigurationCatalogReader _reader;
    public ListConfigurationSettingsByUnitHandler(IConfigurationCatalogReader reader) => _reader = reader;
    public Task<IReadOnlyList<ConfigurationSettingDto>> HandleAsync(
        ListConfigurationSettingsByUnitQuery query, CancellationToken cancellationToken = default)
        => _reader.ListByUnitAsync(query.DeployableUnitId, cancellationToken);
}

public sealed record GetConfigurationSettingByIdQuery(Guid Id);

public sealed class GetConfigurationSettingByIdHandler
{
    private readonly IConfigurationCatalogReader _reader;
    public GetConfigurationSettingByIdHandler(IConfigurationCatalogReader reader) => _reader = reader;
    public Task<ConfigurationSettingDto?> HandleAsync(
        GetConfigurationSettingByIdQuery query, CancellationToken cancellationToken = default)
        => _reader.GetByIdAsync(query.Id, cancellationToken);
}

public sealed record GetConfigurationSettingHistoryQuery(Guid SettingId);

public sealed class GetConfigurationSettingHistoryHandler
{
    private readonly IConfigurationCatalogReader _reader;
    public GetConfigurationSettingHistoryHandler(IConfigurationCatalogReader reader) => _reader = reader;
    public Task<IReadOnlyList<ConfigurationSettingHistoryDto>> HandleAsync(
        GetConfigurationSettingHistoryQuery query, CancellationToken cancellationToken = default)
        => _reader.GetHistoryAsync(query.SettingId, cancellationToken);
}
