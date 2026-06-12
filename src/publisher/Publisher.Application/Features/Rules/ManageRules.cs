using FluentValidation;
using Publisher.Contracts.Rules;
using Publisher.Domain.Abstractions;
using Publisher.Domain.Registries;
using Publisher.Domain.Rules;

namespace Publisher.Application.Features.Rules;

// ---- Create -------------------------------------------------------------------------------------

public sealed record CreateRuleCommand(
    string Name,
    RuleTriggerDto Trigger,
    RuleActionDto Action,
    Guid TargetRegistryId,
    Guid? RepositoryId,
    string? ContainerNamePattern,
    bool RequirePublishable,
    string? RequiredChannelName);

public sealed class CreateRuleValidator : AbstractValidator<CreateRuleCommand>
{
    public CreateRuleValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.TargetRegistryId).NotEmpty();
        RuleFor(x => x.ContainerNamePattern).MaximumLength(300);
        RuleFor(x => x.RequiredChannelName).MaximumLength(200);
    }
}

public sealed class CreateRuleHandler
{
    private readonly IAutomationRuleRepository _rules;
    private readonly IRemoteRegistryRepository _registries;
    private readonly IUnitOfWork _uow;
    private readonly TimeProvider _clock;

    public CreateRuleHandler(
        IAutomationRuleRepository rules,
        IRemoteRegistryRepository registries,
        IUnitOfWork uow,
        TimeProvider clock)
    {
        _rules = rules;
        _registries = registries;
        _uow = uow;
        _clock = clock;
    }

    public async Task<AutomationRuleDto> HandleAsync(CreateRuleCommand cmd, CancellationToken cancellationToken = default)
    {
        if (await _rules.FindByNameAsync(cmd.Name, cancellationToken).ConfigureAwait(false) is not null)
            throw new InvalidOperationException($"A rule named '{cmd.Name}' already exists.");
        if (await _registries.GetByIdAsync(cmd.TargetRegistryId, cancellationToken).ConfigureAwait(false) is null)
            throw new InvalidOperationException($"Target registry {cmd.TargetRegistryId} does not exist.");

        var rule = new AutomationRule(
            id: Guid.NewGuid(),
            name: cmd.Name,
            trigger: cmd.Trigger.ToDomain(),
            action: cmd.Action.ToDomain(),
            targetRegistryId: cmd.TargetRegistryId,
            repositoryId: cmd.RepositoryId,
            containerNamePattern: cmd.ContainerNamePattern,
            requirePublishable: cmd.RequirePublishable,
            requiredChannelName: cmd.RequiredChannelName,
            createdAtUtc: _clock.GetUtcNow());

        await _rules.AddAsync(rule, cancellationToken).ConfigureAwait(false);
        await _uow.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return rule.ToDto();
    }
}

// ---- Update -------------------------------------------------------------------------------------

public sealed record UpdateRuleCommand(
    Guid RuleId,
    RuleTriggerDto Trigger,
    RuleActionDto Action,
    Guid TargetRegistryId,
    Guid? RepositoryId,
    string? ContainerNamePattern,
    bool RequirePublishable,
    string? RequiredChannelName);

public sealed class UpdateRuleValidator : AbstractValidator<UpdateRuleCommand>
{
    public UpdateRuleValidator()
    {
        RuleFor(x => x.RuleId).NotEmpty();
        RuleFor(x => x.TargetRegistryId).NotEmpty();
        RuleFor(x => x.ContainerNamePattern).MaximumLength(300);
        RuleFor(x => x.RequiredChannelName).MaximumLength(200);
    }
}

public sealed class UpdateRuleHandler
{
    private readonly IAutomationRuleRepository _rules;
    private readonly IRemoteRegistryRepository _registries;
    private readonly IUnitOfWork _uow;
    private readonly TimeProvider _clock;

    public UpdateRuleHandler(
        IAutomationRuleRepository rules,
        IRemoteRegistryRepository registries,
        IUnitOfWork uow,
        TimeProvider clock)
    {
        _rules = rules;
        _registries = registries;
        _uow = uow;
        _clock = clock;
    }

    public async Task HandleAsync(UpdateRuleCommand cmd, CancellationToken cancellationToken = default)
    {
        var rule = await _rules.GetByIdAsync(cmd.RuleId, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Rule {cmd.RuleId} not found.");
        if (await _registries.GetByIdAsync(cmd.TargetRegistryId, cancellationToken).ConfigureAwait(false) is null)
            throw new InvalidOperationException($"Target registry {cmd.TargetRegistryId} does not exist.");

        rule.UpdateDefinition(
            cmd.Trigger.ToDomain(), cmd.Action.ToDomain(), cmd.TargetRegistryId,
            cmd.RepositoryId, cmd.ContainerNamePattern, cmd.RequirePublishable, cmd.RequiredChannelName,
            _clock.GetUtcNow());

        await _uow.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}

// ---- Activation / Delete ------------------------------------------------------------------------

public sealed record ChangeRuleActivationCommand(Guid RuleId, bool Enabled);

public sealed class ChangeRuleActivationHandler
{
    private readonly IAutomationRuleRepository _rules;
    private readonly IUnitOfWork _uow;
    private readonly TimeProvider _clock;

    public ChangeRuleActivationHandler(IAutomationRuleRepository rules, IUnitOfWork uow, TimeProvider clock)
    {
        _rules = rules;
        _uow = uow;
        _clock = clock;
    }

    public async Task HandleAsync(ChangeRuleActivationCommand cmd, CancellationToken cancellationToken = default)
    {
        var rule = await _rules.GetByIdAsync(cmd.RuleId, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Rule {cmd.RuleId} not found.");
        rule.ChangeActivation(cmd.Enabled, _clock.GetUtcNow());
        await _uow.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}

public sealed record DeleteRuleCommand(Guid RuleId);

public sealed class DeleteRuleHandler
{
    private readonly IAutomationRuleRepository _rules;
    private readonly IUnitOfWork _uow;

    public DeleteRuleHandler(IAutomationRuleRepository rules, IUnitOfWork uow)
    {
        _rules = rules;
        _uow = uow;
    }

    public async Task HandleAsync(DeleteRuleCommand cmd, CancellationToken cancellationToken = default)
    {
        var rule = await _rules.GetByIdAsync(cmd.RuleId, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Rule {cmd.RuleId} not found.");
        _rules.Remove(rule);
        await _uow.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
