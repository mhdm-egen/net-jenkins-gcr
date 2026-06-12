using System.Text.RegularExpressions;
using Publisher.Domain.Common;
using Publisher.Domain.Rules.Events;

namespace Publisher.Domain.Rules;

/// <summary>
/// A user-defined "when this event happens, do this action" rule — the opt-in switch for
/// automated pushing. With no enabled rule matching a published container, nothing is pushed.
/// A rule targets one <see cref="Registries.RemoteRegistry"/> and may narrow what it applies to
/// via an optional filter (source repository, container-name glob, and/or a publishable-channel
/// requirement).
/// </summary>
public sealed class AutomationRule : AggregateRoot<Guid>
{
    public string Name { get; private set; }
    public bool Enabled { get; private set; }
    public RuleTrigger Trigger { get; private set; }
    public RuleAction Action { get; private set; }

    /// <summary>The registry this rule pushes to.</summary>
    public Guid TargetRegistryId { get; private set; }

    // --- Filter (all optional; null/empty means "no constraint on this dimension") ---

    /// <summary>Only containers from this CI source repository. Null = any repository.</summary>
    public Guid? RepositoryId { get; private set; }

    /// <summary>Glob (<c>*</c>/<c>?</c>) matched against the container name. Null/empty = any name.</summary>
    public string? ContainerNamePattern { get; private set; }

    /// <summary>If true, only push containers that are currently tagged publishable under a channel.</summary>
    public bool RequirePublishable { get; private set; }

    /// <summary>
    /// When <see cref="RequirePublishable"/> is true, optionally require that specific channel name.
    /// Null = any channel satisfies the requirement.
    /// </summary>
    public string? RequiredChannelName { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }

    private AutomationRule() => Name = string.Empty;

    public AutomationRule(
        Guid id,
        string name,
        RuleTrigger trigger,
        RuleAction action,
        Guid targetRegistryId,
        Guid? repositoryId,
        string? containerNamePattern,
        bool requirePublishable,
        string? requiredChannelName,
        DateTimeOffset createdAtUtc)
    {
        if (id == Guid.Empty) throw new ArgumentException("Id cannot be empty.", nameof(id));
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Name cannot be empty.", nameof(name));
        if (targetRegistryId == Guid.Empty) throw new ArgumentException("TargetRegistryId cannot be empty.", nameof(targetRegistryId));

        Id = id;
        Name = name.Trim();
        Trigger = trigger;
        Action = action;
        TargetRegistryId = targetRegistryId;
        ApplyFilter(repositoryId, containerNamePattern, requirePublishable, requiredChannelName);
        Enabled = true;
        CreatedAtUtc = createdAtUtc;
        UpdatedAtUtc = createdAtUtc;

        RaiseEvent(new AutomationRuleCreated(Id, Name, Trigger, Action, TargetRegistryId, createdAtUtc));
    }

    public void UpdateDefinition(
        RuleTrigger trigger,
        RuleAction action,
        Guid targetRegistryId,
        Guid? repositoryId,
        string? containerNamePattern,
        bool requirePublishable,
        string? requiredChannelName,
        DateTimeOffset occurredAtUtc)
    {
        if (targetRegistryId == Guid.Empty) throw new ArgumentException("TargetRegistryId cannot be empty.", nameof(targetRegistryId));

        Trigger = trigger;
        Action = action;
        TargetRegistryId = targetRegistryId;
        ApplyFilter(repositoryId, containerNamePattern, requirePublishable, requiredChannelName);
        UpdatedAtUtc = occurredAtUtc;

        RaiseEvent(new AutomationRuleUpdated(Id, Name, Trigger, Action, TargetRegistryId, occurredAtUtc));
    }

    public void ChangeActivation(bool enabled, DateTimeOffset occurredAtUtc)
    {
        if (Enabled == enabled) return;
        Enabled = enabled;
        UpdatedAtUtc = occurredAtUtc;
        RaiseEvent(new AutomationRuleActivationChanged(Id, enabled, occurredAtUtc));
    }

    /// <summary>
    /// Pure match test against a published container. <paramref name="channelNames"/> is the set of
    /// channels the container is currently bound to (empty if not publishable).
    /// </summary>
    public bool Matches(Guid repositoryId, string containerName, IReadOnlyCollection<string> channelNames)
    {
        if (!Enabled) return false;
        if (RepositoryId is { } rid && rid != repositoryId) return false;
        if (!string.IsNullOrEmpty(ContainerNamePattern) && !GlobMatch(ContainerNamePattern, containerName)) return false;
        if (RequirePublishable)
        {
            if (channelNames.Count == 0) return false;
            if (!string.IsNullOrEmpty(RequiredChannelName) &&
                !channelNames.Contains(RequiredChannelName, StringComparer.OrdinalIgnoreCase))
                return false;
        }
        return true;
    }

    private void ApplyFilter(Guid? repositoryId, string? containerNamePattern, bool requirePublishable, string? requiredChannelName)
    {
        RepositoryId = repositoryId == Guid.Empty ? null : repositoryId;
        ContainerNamePattern = string.IsNullOrWhiteSpace(containerNamePattern) ? null : containerNamePattern.Trim();
        RequirePublishable = requirePublishable;
        RequiredChannelName = string.IsNullOrWhiteSpace(requiredChannelName) ? null : requiredChannelName.Trim();
    }

    /// <summary>Simple <c>*</c>/<c>?</c> glob match, case-insensitive, anchored.</summary>
    private static bool GlobMatch(string pattern, string value)
    {
        var rx = "^" + Regex.Escape(pattern).Replace("\\*", ".*").Replace("\\?", ".") + "$";
        return Regex.IsMatch(value, rx, RegexOptions.IgnoreCase);
    }
}
