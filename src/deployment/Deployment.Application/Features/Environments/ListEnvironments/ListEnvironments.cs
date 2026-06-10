using Deployment.Contracts.Environments;

namespace Deployment.Application.Features.Environments.ListEnvironments;

public interface IEnvironmentCatalogReader
{
    Task<IReadOnlyList<EnvironmentDto>> ListAsync(CancellationToken cancellationToken = default);
    Task<EnvironmentDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
}

public sealed record ListEnvironmentsQuery();

public sealed class ListEnvironmentsHandler
{
    private readonly IEnvironmentCatalogReader _reader;
    public ListEnvironmentsHandler(IEnvironmentCatalogReader reader) => _reader = reader;
    public Task<IReadOnlyList<EnvironmentDto>> HandleAsync(ListEnvironmentsQuery query, CancellationToken cancellationToken = default)
        => _reader.ListAsync(cancellationToken);
}

public sealed record GetEnvironmentByIdQuery(Guid Id);

public sealed class GetEnvironmentByIdHandler
{
    private readonly IEnvironmentCatalogReader _reader;
    public GetEnvironmentByIdHandler(IEnvironmentCatalogReader reader) => _reader = reader;
    public Task<EnvironmentDto?> HandleAsync(GetEnvironmentByIdQuery query, CancellationToken cancellationToken = default)
        => _reader.GetByIdAsync(query.Id, cancellationToken);
}
