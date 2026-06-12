using Microsoft.EntityFrameworkCore;
using Publisher.Domain.Promotions;

namespace Publisher.Infrastructure.Persistence.Repositories;

public sealed class PromotionRepository
    : EfRepository<Promotion, Guid>, IPromotionRepository
{
    public PromotionRepository(PublisherDbContext db) : base(db) { }

    public Task<bool> ExistsActiveAsync(Guid containerId, Guid registryId, CancellationToken cancellationToken = default)
        => Set.AnyAsync(
            p => p.ContainerId == containerId
                 && p.RegistryId == registryId
                 && (p.Status == PromotionStatus.Pending || p.Status == PromotionStatus.Succeeded),
            cancellationToken);
}
