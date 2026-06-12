namespace Publisher.Contracts.Rules;

public enum RuleTriggerDto
{
    ContainerPublished = 0,
}

public enum RuleActionDto
{
    PushToRemote = 0,
}

public sealed record AutomationRuleDto(
    Guid Id,
    string Name,
    bool Enabled,
    RuleTriggerDto Trigger,
    RuleActionDto Action,
    Guid TargetRegistryId,
    Guid? RepositoryId,
    string? ContainerNamePattern,
    bool RequirePublishable,
    string? RequiredChannelName,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);

public sealed record CreateRuleRequest(
    string Name,
    RuleTriggerDto Trigger,
    RuleActionDto Action,
    Guid TargetRegistryId,
    Guid? RepositoryId,
    string? ContainerNamePattern,
    bool RequirePublishable,
    string? RequiredChannelName);

public sealed record UpdateRuleRequest(
    RuleTriggerDto Trigger,
    RuleActionDto Action,
    Guid TargetRegistryId,
    Guid? RepositoryId,
    string? ContainerNamePattern,
    bool RequirePublishable,
    string? RequiredChannelName);
