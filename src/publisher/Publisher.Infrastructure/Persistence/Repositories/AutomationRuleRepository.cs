using Microsoft.EntityFrameworkCore;
using Publisher.Domain.Rules;

namespace Publisher.Infrastructure.Persistence.Repositories;

public sealed class AutomationRuleRepository
    : EfRepository<AutomationRule, Guid>, IAutomationRuleRepository
{
    public AutomationRuleRepository(PublisherDbContext db) : base(db) { }

    public Task<AutomationRule?> FindByNameAsync(string name, CancellationToken cancellationToken = default)
    {
        var n = name.Trim();
        return Set.FirstOrDefaultAsync(r => r.Name == n, cancellationToken);
    }

    public async Task<IReadOnlyList<AutomationRule>> ListEnabledByTriggerAsync(RuleTrigger trigger, CancellationToken cancellationToken = default)
        => await Set.Where(r => r.Enabled && r.Trigger == trigger).ToListAsync(cancellationToken).ConfigureAwait(false);

    public async Task<IReadOnlyList<AutomationRule>> ListByRegistryAsync(Guid registryId, CancellationToken cancellationToken = default)
        => await Set.Where(r => r.TargetRegistryId == registryId).ToListAsync(cancellationToken).ConfigureAwait(false);
}
