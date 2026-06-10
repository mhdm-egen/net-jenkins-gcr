using Deployment.Contracts.Catalog;

namespace Deployment.Application.Features.Catalog.Applications;

public sealed record ListApplicationsQuery(bool? OnlyActive);

public interface IApplicationCatalogReader
{
    Task<IReadOnlyList<ApplicationDto>> ListAsync(bool? onlyActive, CancellationToken cancellationToken = default);
    Task<ApplicationDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
}

public sealed class ListApplicationsHandler
{
    private readonly IApplicationCatalogReader _reader;
    public ListApplicationsHandler(IApplicationCatalogReader reader) => _reader = reader;

    public Task<IReadOnlyList<ApplicationDto>> HandleAsync(ListApplicationsQuery query, CancellationToken cancellationToken = default)
        => _reader.ListAsync(query.OnlyActive, cancellationToken);
}

public sealed record GetApplicationByIdQuery(Guid Id);

public sealed class GetApplicationByIdHandler
{
    private readonly IApplicationCatalogReader _reader;
    public GetApplicationByIdHandler(IApplicationCatalogReader reader) => _reader = reader;

    public Task<ApplicationDto?> HandleAsync(GetApplicationByIdQuery query, CancellationToken cancellationToken = default)
        => _reader.GetByIdAsync(query.Id, cancellationToken);
}
