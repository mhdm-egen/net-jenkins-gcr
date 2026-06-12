using Publisher.Domain.Common;

namespace Publisher.Domain.Rules.Events;

public sealed record AutomationRuleCreated(
    Guid RuleId,
    string Name,
    RuleTrigger Trigger,
    RuleAction Action,
    Guid TargetRegistryId,
    DateTimeOffset OccurredAtUtc) : IDomainEvent;

public sealed record AutomationRuleUpdated(
    Guid RuleId,
    string Name,
    RuleTrigger Trigger,
    RuleAction Action,
    Guid TargetRegistryId,
    DateTimeOffset OccurredAtUtc) : IDomainEvent;

public sealed record AutomationRuleActivationChanged(
    Guid RuleId,
    bool Enabled,
    DateTimeOffset OccurredAtUtc) : IDomainEvent;
