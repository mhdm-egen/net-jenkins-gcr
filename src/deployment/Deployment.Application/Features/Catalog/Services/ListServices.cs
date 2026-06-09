using Deployment.Contracts.Catalog;

namespace Deployment.Application.Features.Catalog.Services;

/// <summary>
/// Read-model port: list all services. The handler delegates to the reader so
/// the projection can use a flat SELECT without pulling whole aggregates.
/// </summary>
public sealed record ListServicesQuery(bool? OnlyActive);

public interface IServiceCatalogReader
{
    Task<IReadOnlyList<ServiceDto>> ListAsync(bool? onlyActive, CancellationToken cancellationToken = default);
    Task<ServiceDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
}

public sealed class ListServicesHandler
{
    private readonly IServiceCatalogReader _reader;
    public ListServicesHandler(IServiceCatalogReader reader) => _reader = reader;

    public Task<IReadOnlyList<ServiceDto>> HandleAsync(ListServicesQuery query, CancellationToken cancellationToken = default)
        => _reader.ListAsync(query.OnlyActive, cancellationToken);
}

public sealed record GetServiceByIdQuery(Guid Id);

public sealed class GetServiceByIdHandler
{
    private readonly IServiceCatalogReader _reader;
    public GetServiceByIdHandler(IServiceCatalogReader reader) => _reader = reader;

    public Task<ServiceDto?> HandleAsync(GetServiceByIdQuery query, CancellationToken cancellationToken = default)
        => _reader.GetByIdAsync(query.Id, cancellationToken);
}
