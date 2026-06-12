using Publisher.Domain.Abstractions;

namespace Publisher.Domain.Rules;

public interface IAutomationRuleRepository : IRepository<AutomationRule, Guid>
{
    Task<AutomationRule?> FindByNameAsync(string name, CancellationToken cancellationToken = default);

    /// <summary>All enabled rules for a given trigger — the candidate set evaluated on each event.</summary>
    Task<IReadOnlyList<AutomationRule>> ListEnabledByTriggerAsync(RuleTrigger trigger, CancellationToken cancellationToken = default);

    /// <summary>Rules that target a given registry (used to block deletion of a referenced registry).</summary>
    Task<IReadOnlyList<AutomationRule>> ListByRegistryAsync(Guid registryId, CancellationToken cancellationToken = default);
}
