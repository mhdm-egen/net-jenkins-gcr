using Jenkins.Contracts.Repositories;

namespace Jenkins.Application.Features.Repositories;

/// <summary>
/// Read-model port for the repository catalog. The handler delegates to the reader
/// so projections can run as flat SELECTs without materializing aggregates.
/// </summary>
public interface IRepositoryCatalogReader
{
    Task<IReadOnlyList<RepositoryDto>> ListAsync(bool? onlyActive, CancellationToken cancellationToken = default);
    Task<RepositoryDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
}

public sealed record ListRepositoriesQuery(bool? OnlyActive);

public sealed class ListRepositoriesHandler
{
    private readonly IRepositoryCatalogReader _reader;
    public ListRepositoriesHandler(IRepositoryCatalogReader reader) => _reader = reader;

    public Task<IReadOnlyList<RepositoryDto>> HandleAsync(ListRepositoriesQuery query, CancellationToken cancellationToken = default)
        => _reader.ListAsync(query.OnlyActive, cancellationToken);
}

public sealed record GetRepositoryByIdQuery(Guid Id);

public sealed class GetRepositoryByIdHandler
{
    private readonly IRepositoryCatalogReader _reader;
    public GetRepositoryByIdHandler(IRepositoryCatalogReader reader) => _reader = reader;

    public Task<RepositoryDto?> HandleAsync(GetRepositoryByIdQuery query, CancellationToken cancellationToken = default)
        => _reader.GetByIdAsync(query.Id, cancellationToken);
}
