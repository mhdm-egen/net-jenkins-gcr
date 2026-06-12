using Publisher.Contracts.Rules;
using Publisher.Domain.Rules;

namespace Publisher.Application.Features.Rules;

internal static class RuleMapping
{
    public static AutomationRuleDto ToDto(this AutomationRule r) => new(
        Id: r.Id,
        Name: r.Name,
        Enabled: r.Enabled,
        Trigger: (RuleTriggerDto)(int)r.Trigger,
        Action: (RuleActionDto)(int)r.Action,
        TargetRegistryId: r.TargetRegistryId,
        RepositoryId: r.RepositoryId,
        ContainerNamePattern: r.ContainerNamePattern,
        RequirePublishable: r.RequirePublishable,
        RequiredChannelName: r.RequiredChannelName,
        CreatedAtUtc: r.CreatedAtUtc,
        UpdatedAtUtc: r.UpdatedAtUtc);

    public static RuleTrigger ToDomain(this RuleTriggerDto t) => (RuleTrigger)(int)t;
    public static RuleAction ToDomain(this RuleActionDto a) => (RuleAction)(int)a;
}
