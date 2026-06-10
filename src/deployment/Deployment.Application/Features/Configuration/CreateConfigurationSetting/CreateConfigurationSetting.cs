using Deployment.Application.Features.Configuration;
using Deployment.Contracts.Configuration;
using Deployment.Domain.Abstractions;
using Deployment.Domain.Configuration;
using FluentValidation;

namespace Deployment.Application.Features.Configuration.CreateConfigurationSetting;

public sealed record CreateConfigurationSettingCommand(
    Guid Id,
    Guid DeployableUnitId,
    Guid? EnvironmentId,
    string Key,
    bool IsSecret,
    string? Value,
    string? SecretReference,
    ConfigurationValueTypeDto ValueType,
    string ChangedByPrincipal);

public sealed class CreateConfigurationSettingValidator : AbstractValidator<CreateConfigurationSettingCommand>
{
    public CreateConfigurationSettingValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.DeployableUnitId).NotEmpty();
        RuleFor(x => x.Key).NotEmpty().MaximumLength(500);
        RuleFor(x => x.ChangedByPrincipal).NotEmpty().MaximumLength(200);

        // Boundary check that mirrors the domain dichotomy so we return 400
        // instead of the domain's 409 InvalidOperationException.
        When(x => x.IsSecret, () =>
        {
            RuleFor(x => x.SecretReference).NotEmpty()
                .WithMessage("Secret settings require a SecretReference.");
            RuleFor(x => x.Value).Empty()
                .WithMessage("Secret settings must have an empty Value (only the reference is stored).");
        }).Otherwise(() =>
        {
            RuleFor(x => x.Value).NotNull()
                .WithMessage("Plain settings require a non-null Value (empty string is allowed).");
            RuleFor(x => x.SecretReference).Empty()
                .WithMessage("Plain settings must have an empty SecretReference.");
        });
    }
}

public sealed class CreateConfigurationSettingHandler
{
    private readonly IConfigurationSettingRepository _settings;
    private readonly IUnitOfWork _uow;
    private readonly TimeProvider _clock;

    public CreateConfigurationSettingHandler(
        IConfigurationSettingRepository settings, IUnitOfWork uow, TimeProvider clock)
    {
        _settings = settings;
        _uow = uow;
        _clock = clock;
    }

    public async Task<Guid> HandleAsync(CreateConfigurationSettingCommand cmd, CancellationToken cancellationToken = default)
    {
        // The unique index is (DeployableUnitId, EnvironmentId, Key); pre-check
        // so the API returns 409 instead of a SqlException 2601.
        var existing = await _settings.FindAsync(cmd.DeployableUnitId, cmd.EnvironmentId, cmd.Key, cancellationToken)
            .ConfigureAwait(false);
        if (existing is not null)
            throw new InvalidOperationException(
                $"A configuration setting for unit {cmd.DeployableUnitId}, env {cmd.EnvironmentId?.ToString() ?? "(default)"}, key '{cmd.Key}' already exists.");

        var now = _clock.GetUtcNow();
        ConfigurationSetting created = cmd.IsSecret
            ? ConfigurationSetting.CreateSecret(
                cmd.Id, cmd.DeployableUnitId, cmd.EnvironmentId, cmd.Key,
                cmd.SecretReference!, cmd.ValueType.ToDomain(), cmd.ChangedByPrincipal, now)
            : ConfigurationSetting.CreatePlain(
                cmd.Id, cmd.DeployableUnitId, cmd.EnvironmentId, cmd.Key,
                cmd.Value!, cmd.ValueType.ToDomain(), cmd.ChangedByPrincipal, now);

        await _settings.AddAsync(created, cancellationToken).ConfigureAwait(false);
        await _uow.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return created.Id;
    }
}
