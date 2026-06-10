using Deployment.Domain.Abstractions;
using Deployment.Domain.Configuration;
using FluentValidation;

namespace Deployment.Application.Features.Configuration.DeleteConfigurationSetting;

public sealed record DeleteConfigurationSettingCommand(
    Guid SettingId,
    string ChangedByPrincipal);

public sealed class DeleteConfigurationSettingValidator : AbstractValidator<DeleteConfigurationSettingCommand>
{
    public DeleteConfigurationSettingValidator()
    {
        RuleFor(x => x.SettingId).NotEmpty();
        RuleFor(x => x.ChangedByPrincipal).NotEmpty().MaximumLength(200);
    }
}

public sealed class DeleteConfigurationSettingHandler
{
    private readonly IConfigurationSettingRepository _settings;
    private readonly IUnitOfWork _uow;
    private readonly TimeProvider _clock;

    public DeleteConfigurationSettingHandler(
        IConfigurationSettingRepository settings, IUnitOfWork uow, TimeProvider clock)
    {
        _settings = settings;
        _uow = uow;
        _clock = clock;
    }

    public async Task HandleAsync(DeleteConfigurationSettingCommand cmd, CancellationToken cancellationToken = default)
    {
        var setting = await _settings.GetByIdAsync(cmd.SettingId, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Configuration setting {cmd.SettingId} not found.");

        // MarkForDeletion raises the Deleted event; Remove queues the row for
        // physical deletion in the same UoW so the event reflects reality.
        setting.MarkForDeletion(cmd.ChangedByPrincipal, _clock.GetUtcNow());
        _settings.Remove(setting);
        await _uow.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
