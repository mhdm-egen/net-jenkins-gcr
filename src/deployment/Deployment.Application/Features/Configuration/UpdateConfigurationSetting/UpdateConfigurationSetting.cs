using Deployment.Application.Features.Configuration;
using Deployment.Contracts.Configuration;
using Deployment.Domain.Abstractions;
using Deployment.Domain.Configuration;
using FluentValidation;

namespace Deployment.Application.Features.Configuration.UpdateConfigurationSetting;

/// <summary>
/// Update the state of an existing setting. Supports any combination:
/// plain→plain, plain→secret, secret→plain, secret→secret. The handler
/// computes the diff inside the domain entity which emits the change event.
/// </summary>
public sealed record UpdateConfigurationSettingCommand(
    Guid SettingId,
    bool IsSecret,
    string? Value,
    string? SecretReference,
    ConfigurationValueTypeDto ValueType,
    string ChangedByPrincipal);

public sealed class UpdateConfigurationSettingValidator : AbstractValidator<UpdateConfigurationSettingCommand>
{
    public UpdateConfigurationSettingValidator()
    {
        RuleFor(x => x.SettingId).NotEmpty();
        RuleFor(x => x.ChangedByPrincipal).NotEmpty().MaximumLength(200);

        When(x => x.IsSecret, () =>
        {
            RuleFor(x => x.SecretReference).NotEmpty()
                .WithMessage("Secret settings require a SecretReference.");
            RuleFor(x => x.Value).Empty()
                .WithMessage("Secret settings must have an empty Value.");
        }).Otherwise(() =>
        {
            RuleFor(x => x.Value).NotNull()
                .WithMessage("Plain settings require a non-null Value.");
            RuleFor(x => x.SecretReference).Empty()
                .WithMessage("Plain settings must have an empty SecretReference.");
        });
    }
}

public sealed class UpdateConfigurationSettingHandler
{
    private readonly IConfigurationSettingRepository _settings;
    private readonly IUnitOfWork _uow;
    private readonly TimeProvider _clock;

    public UpdateConfigurationSettingHandler(
        IConfigurationSettingRepository settings, IUnitOfWork uow, TimeProvider clock)
    {
        _settings = settings;
        _uow = uow;
        _clock = clock;
    }

    public async Task HandleAsync(UpdateConfigurationSettingCommand cmd, CancellationToken cancellationToken = default)
    {
        var setting = await _settings.GetByIdAsync(cmd.SettingId, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Configuration setting {cmd.SettingId} not found.");

        setting.Update(
            newValue: cmd.Value,
            newIsSecret: cmd.IsSecret,
            newSecretReference: cmd.SecretReference,
            newValueType: cmd.ValueType.ToDomain(),
            changedByPrincipal: cmd.ChangedByPrincipal,
            occurredAtUtc: _clock.GetUtcNow());

        await _uow.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
