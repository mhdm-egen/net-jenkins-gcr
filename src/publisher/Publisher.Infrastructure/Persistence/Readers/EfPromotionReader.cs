using Microsoft.EntityFrameworkCore;
using Publisher.Application.Features.Promotions;
using Publisher.Contracts.Promotions;

namespace Publisher.Infrastructure.Persistence.Readers;

internal sealed class EfPromotionReader : IPromotionReader
{
    private readonly PublisherDbContext _db;
    public EfPromotionReader(PublisherDbContext db) => _db = db;

    public async Task<IReadOnlyList<PromotionDto>> ListAsync(Guid? containerId, Guid? registryId, CancellationToken cancellationToken = default)
        => await _db.Promotions.AsNoTracking()
            .Where(p => (!containerId.HasValue || p.ContainerId == containerId.Value)
                        && (!registryId.HasValue || p.RegistryId == registryId.Value))
            .OrderByDescending(p => p.RequestedAtUtc)
            .Select(p => new PromotionDto(
                p.Id, p.ContainerId, p.RegistryId, p.RegistryName, p.RuleId, p.TriggeredBy,
                p.SourceRef, p.RemoteRef, p.RepositoryId, p.ContainerName, p.Version,
                (PromotionStatusDto)(int)p.Status, p.FailureReason, p.RequestedAtUtc, p.CompletedAtUtc))
            .ToListAsync(cancellationToken).ConfigureAwait(false);

    public async Task<PromotionDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => await _db.Promotions.AsNoTracking()
            .Where(p => p.Id == id)
            .Select(p => new PromotionDto(
                p.Id, p.ContainerId, p.RegistryId, p.RegistryName, p.RuleId, p.TriggeredBy,
                p.SourceRef, p.RemoteRef, p.RepositoryId, p.ContainerName, p.Version,
                (PromotionStatusDto)(int)p.Status, p.FailureReason, p.RequestedAtUtc, p.CompletedAtUtc))
            .FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
}
