namespace Publisher.Domain.Rules;

/// <summary>The event that fires a rule. Only container-published is supported today.</summary>
public enum RuleTrigger
{
    ContainerPublished = 0,
}

/// <summary>What a fired rule does. Only push-to-remote is supported today.</summary>
public enum RuleAction
{
    PushToRemote = 0,
}
