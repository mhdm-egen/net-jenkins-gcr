using Publisher.Contracts.Promotions;

namespace Publisher.Application.Features.Promotions;

public interface IPromotionReader
{
    Task<IReadOnlyList<PromotionDto>> ListAsync(Guid? containerId, Guid? registryId, CancellationToken cancellationToken = default);
    Task<PromotionDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
}

public sealed record ListPromotionsQuery(Guid? ContainerId, Guid? RegistryId);

public sealed class ListPromotionsHandler
{
    private readonly IPromotionReader _reader;
    public ListPromotionsHandler(IPromotionReader reader) => _reader = reader;

    public Task<IReadOnlyList<PromotionDto>> HandleAsync(ListPromotionsQuery query, CancellationToken cancellationToken = default)
        => _reader.ListAsync(query.ContainerId, query.RegistryId, cancellationToken);
}

public sealed record GetPromotionByIdQuery(Guid Id);

public sealed class GetPromotionByIdHandler
{
    private readonly IPromotionReader _reader;
    public GetPromotionByIdHandler(IPromotionReader reader) => _reader = reader;

    public Task<PromotionDto?> HandleAsync(GetPromotionByIdQuery query, CancellationToken cancellationToken = default)
        => _reader.GetByIdAsync(query.Id, cancellationToken);
}
