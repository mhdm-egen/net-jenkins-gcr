using Publisher.Contracts.Registries;

namespace Publisher.Application.Features.Registries;

public interface IRegistryReader
{
    Task<IReadOnlyList<RemoteRegistryDto>> ListAsync(CancellationToken cancellationToken = default);
    Task<RemoteRegistryDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
}

public sealed record ListRegistriesQuery;

public sealed class ListRegistriesHandler
{
    private readonly IRegistryReader _reader;
    public ListRegistriesHandler(IRegistryReader reader) => _reader = reader;

    public Task<IReadOnlyList<RemoteRegistryDto>> HandleAsync(ListRegistriesQuery query, CancellationToken cancellationToken = default)
        => _reader.ListAsync(cancellationToken);
}

public sealed record GetRegistryByIdQuery(Guid Id);

public sealed class GetRegistryByIdHandler
{
    private readonly IRegistryReader _reader;
    public GetRegistryByIdHandler(IRegistryReader reader) => _reader = reader;

    public Task<RemoteRegistryDto?> HandleAsync(GetRegistryByIdQuery query, CancellationToken cancellationToken = default)
        => _reader.GetByIdAsync(query.Id, cancellationToken);
}
