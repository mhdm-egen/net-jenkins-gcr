using Microsoft.EntityFrameworkCore;
using Publisher.Application.Features.Rules;
using Publisher.Contracts.Rules;

namespace Publisher.Infrastructure.Persistence.Readers;

public sealed class EfRuleReader : IRuleReader
{
    private readonly PublisherDbContext _db;
    public EfRuleReader(PublisherDbContext db) => _db = db;

    public async Task<IReadOnlyList<AutomationRuleDto>> ListAsync(CancellationToken cancellationToken = default)
        => await _db.Rules.AsNoTracking()
            .OrderBy(r => r.Name)
            .Select(r => new AutomationRuleDto(
                r.Id, r.Name, r.Enabled, (RuleTriggerDto)(int)r.Trigger, (RuleActionDto)(int)r.Action,
                r.TargetRegistryId, r.RepositoryId, r.ContainerNamePattern, r.RequirePublishable,
                r.RequiredChannelName, r.CreatedAtUtc, r.UpdatedAtUtc))
            .ToListAsync(cancellationToken).ConfigureAwait(false);

    public async Task<AutomationRuleDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => await _db.Rules.AsNoTracking()
            .Where(r => r.Id == id)
            .Select(r => new AutomationRuleDto(
                r.Id, r.Name, r.Enabled, (RuleTriggerDto)(int)r.Trigger, (RuleActionDto)(int)r.Action,
                r.TargetRegistryId, r.RepositoryId, r.ContainerNamePattern, r.RequirePublishable,
                r.RequiredChannelName, r.CreatedAtUtc, r.UpdatedAtUtc))
            .FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
}
