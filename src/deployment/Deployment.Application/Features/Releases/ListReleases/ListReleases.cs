using Deployment.Contracts.Releases;

namespace Deployment.Application.Features.Releases.ListReleases;

/// <summary>
/// Read-model port for the Releases UI / Deployment.Api. Backed by an EF
/// projection in Infrastructure; the Application layer stays persistence-agnostic.
/// </summary>
public interface IReleaseCatalogReader
{
    /// <summary>List releases for a deployable unit (service or application), newest first.</summary>
    Task<IReadOnlyList<ReleaseDto>> ListByUnitAsync(
        Guid deployableUnitId,
        CancellationToken cancellationToken = default);

    Task<ReleaseDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Full transition timeline for one release (newest first). Backs the status-history card.</summary>
    Task<IReadOnlyList<ReleaseStatusChangeDto>> GetStatusHistoryAsync(
        Guid releaseId,
        CancellationToken cancellationToken = default);
}

public sealed record ListReleasesByUnitQuery(Guid DeployableUnitId);

public sealed class ListReleasesByUnitHandler
{
    private readonly IReleaseCatalogReader _reader;
    public ListReleasesByUnitHandler(IReleaseCatalogReader reader) => _reader = reader;
    public Task<IReadOnlyList<ReleaseDto>> HandleAsync(ListReleasesByUnitQuery query, CancellationToken cancellationToken = default)
        => _reader.ListByUnitAsync(query.DeployableUnitId, cancellationToken);
}

public sealed record GetReleaseByIdQuery(Guid Id);

public sealed class GetReleaseByIdHandler
{
    private readonly IReleaseCatalogReader _reader;
    public GetReleaseByIdHandler(IReleaseCatalogReader reader) => _reader = reader;
    public Task<ReleaseDto?> HandleAsync(GetReleaseByIdQuery query, CancellationToken cancellationToken = default)
        => _reader.GetByIdAsync(query.Id, cancellationToken);
}

public sealed record GetReleaseStatusHistoryQuery(Guid ReleaseId);

public sealed class GetReleaseStatusHistoryHandler
{
    private readonly IReleaseCatalogReader _reader;
    public GetReleaseStatusHistoryHandler(IReleaseCatalogReader reader) => _reader = reader;
    public Task<IReadOnlyList<ReleaseStatusChangeDto>> HandleAsync(GetReleaseStatusHistoryQuery query, CancellationToken cancellationToken = default)
        => _reader.GetStatusHistoryAsync(query.ReleaseId, cancellationToken);
}
