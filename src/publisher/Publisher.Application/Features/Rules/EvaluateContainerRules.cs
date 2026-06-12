using Microsoft.Extensions.Logging;
using Publisher.Application.Features.Channels;
using Publisher.Application.Features.Promotions;
using Publisher.Domain.Rules;

namespace Publisher.Application.Features.Rules;

/// <summary>
/// Evaluates the enabled <c>ContainerPublished</c> automation rules against a just-recorded
/// container and requests a promotion for each match. This is the opt-in gate: with no enabled
/// matching rule, nothing is pushed. Invoked from the bus consumer after the container is recorded.
/// </summary>
public sealed record EvaluateContainerRulesCommand(Guid ContainerId, Guid RepositoryId, string ContainerName);

public sealed class EvaluateContainerRulesHandler
{
    private readonly IAutomationRuleRepository _rules;
    private readonly IChannelReader _channels;
    private readonly PromoteContainerHandler _promote;
    private readonly ILogger<EvaluateContainerRulesHandler> _logger;

    public EvaluateContainerRulesHandler(
        IAutomationRuleRepository rules,
        IChannelReader channels,
        PromoteContainerHandler promote,
        ILogger<EvaluateContainerRulesHandler> logger)
    {
        _rules = rules;
        _channels = channels;
        _promote = promote;
        _logger = logger;
    }

    public async Task HandleAsync(EvaluateContainerRulesCommand cmd, CancellationToken cancellationToken = default)
    {
        var rules = await _rules.ListEnabledByTriggerAsync(RuleTrigger.ContainerPublished, cancellationToken).ConfigureAwait(false);
        if (rules.Count == 0) return;

        // Resolve channel membership only if some rule actually needs it.
        IReadOnlyCollection<string> channelNames = Array.Empty<string>();
        if (rules.Any(r => r.RequirePublishable))
            channelNames = await _channels.ListChannelNamesForContainerAsync(cmd.ContainerId, cancellationToken).ConfigureAwait(false);

        foreach (var rule in rules)
        {
            if (!rule.Matches(cmd.RepositoryId, cmd.ContainerName, channelNames)) continue;

            var result = await _promote.HandleAsync(
                new PromoteContainerCommand(cmd.ContainerId, rule.TargetRegistryId, rule.Id, $"rule:{rule.Name}"),
                cancellationToken).ConfigureAwait(false);

            _logger.LogInformation(
                "[rules] Rule '{Rule}' matched container {Container} -> registry {Registry}: {Outcome}.",
                rule.Name, cmd.ContainerName, rule.TargetRegistryId, result.Outcome);
        }
    }
}
