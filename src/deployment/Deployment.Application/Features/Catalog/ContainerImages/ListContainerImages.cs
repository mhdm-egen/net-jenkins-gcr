using Deployment.Contracts.Catalog;

namespace Deployment.Application.Features.Catalog.ContainerImages;

/// <summary>
/// Read-model port: list / get container-image coordinates. The handler delegates to the
/// reader so the projection can use a flat SELECT without pulling whole aggregates.
/// </summary>
public sealed record ListContainerImagesQuery(bool? OnlyActive);

public interface IContainerImageCatalogReader
{
    Task<IReadOnlyList<ContainerImageDto>> ListAsync(bool? onlyActive, CancellationToken cancellationToken = default);
    Task<ContainerImageDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
}

public sealed class ListContainerImagesHandler
{
    private readonly IContainerImageCatalogReader _reader;
    public ListContainerImagesHandler(IContainerImageCatalogReader reader) => _reader = reader;

    public Task<IReadOnlyList<ContainerImageDto>> HandleAsync(ListContainerImagesQuery query, CancellationToken cancellationToken = default)
        => _reader.ListAsync(query.OnlyActive, cancellationToken);
}

public sealed record GetContainerImageByIdQuery(Guid Id);

public sealed class GetContainerImageByIdHandler
{
    private readonly IContainerImageCatalogReader _reader;
    public GetContainerImageByIdHandler(IContainerImageCatalogReader reader) => _reader = reader;

    public Task<ContainerImageDto?> HandleAsync(GetContainerImageByIdQuery query, CancellationToken cancellationToken = default)
        => _reader.GetByIdAsync(query.Id, cancellationToken);
}
