using Publisher.Contracts.Containers;

namespace Publisher.Application.Features.Containers;

/// <summary>
/// Read-model port over the container inventory. The handler delegates to the reader so the
/// projection can use a flat SELECT rather than loading whole aggregates.
/// </summary>
public interface IContainerInventoryReader
{
    Task<IReadOnlyList<PublishableContainerDto>> ListAsync(
        Guid? repositoryId,
        string? containerName,
        CancellationToken cancellationToken = default);

    Task<PublishableContainerDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
}

public sealed record ListContainersQuery(Guid? RepositoryId, string? ContainerName);

public sealed class ListContainersHandler
{
    private readonly IContainerInventoryReader _reader;
    public ListContainersHandler(IContainerInventoryReader reader) => _reader = reader;

    public Task<IReadOnlyList<PublishableContainerDto>> HandleAsync(ListContainersQuery query, CancellationToken cancellationToken = default)
        => _reader.ListAsync(query.RepositoryId, query.ContainerName, cancellationToken);
}

public sealed record GetContainerByIdQuery(Guid Id);

public sealed class GetContainerByIdHandler
{
    private readonly IContainerInventoryReader _reader;
    public GetContainerByIdHandler(IContainerInventoryReader reader) => _reader = reader;

    public Task<PublishableContainerDto?> HandleAsync(GetContainerByIdQuery query, CancellationToken cancellationToken = default)
        => _reader.GetByIdAsync(query.Id, cancellationToken);
}
