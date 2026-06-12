using Publisher.Domain.Abstractions;

namespace Publisher.Domain.Promotions;

public interface IPromotionRepository : IRepository<Promotion, Guid>
{
    /// <summary>
    /// Idempotency check: is there already a succeeded (or in-flight pending) promotion of this
    /// container to this registry? Used to avoid re-pushing the same image.
    /// </summary>
    Task<bool> ExistsActiveAsync(Guid containerId, Guid registryId, CancellationToken cancellationToken = default);
}
