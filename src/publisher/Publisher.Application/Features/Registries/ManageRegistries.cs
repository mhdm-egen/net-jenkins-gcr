using FluentValidation;
using Publisher.Contracts.Registries;
using Publisher.Domain.Abstractions;
using Publisher.Domain.Registries;
using Publisher.Domain.Rules;

namespace Publisher.Application.Features.Registries;

// ---- Create -------------------------------------------------------------------------------------

public sealed record CreateRegistryCommand(
    string Name,
    RegistryProviderDto Provider,
    string RegistryHost,
    string RepositoryPath,
    RegistryAuthMethodDto AuthMethod,
    string? Username,
    string? CredentialSecretRef,
    bool MakeDefault);

public sealed class CreateRegistryValidator : AbstractValidator<CreateRegistryCommand>
{
    public CreateRegistryValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.RegistryHost).NotEmpty().MaximumLength(300);
        RuleFor(x => x.RepositoryPath).MaximumLength(500);
    }
}

public sealed class CreateRegistryHandler
{
    private readonly IRemoteRegistryRepository _registries;
    private readonly IUnitOfWork _uow;
    private readonly TimeProvider _clock;

    public CreateRegistryHandler(IRemoteRegistryRepository registries, IUnitOfWork uow, TimeProvider clock)
    {
        _registries = registries;
        _uow = uow;
        _clock = clock;
    }

    public async Task<RemoteRegistryDto> HandleAsync(CreateRegistryCommand cmd, CancellationToken cancellationToken = default)
    {
        if (await _registries.FindByNameAsync(cmd.Name, cancellationToken).ConfigureAwait(false) is not null)
            throw new InvalidOperationException($"A registry named '{cmd.Name}' already exists.");

        var now = _clock.GetUtcNow();
        var registry = new RemoteRegistry(
            id: Guid.NewGuid(),
            name: cmd.Name,
            provider: cmd.Provider.ToDomain(),
            registryHost: cmd.RegistryHost,
            repositoryPath: cmd.RepositoryPath,
            authMethod: cmd.AuthMethod.ToDomain(),
            username: cmd.Username,
            credentialSecretRef: cmd.CredentialSecretRef,
            createdAtUtc: now);

        await _registries.AddAsync(registry, cancellationToken).ConfigureAwait(false);

        if (cmd.MakeDefault)
        {
            await ClearExistingDefaultsAsync(now, cancellationToken).ConfigureAwait(false);
            registry.SetDefault(true, now);
        }

        await _uow.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return registry.ToDto();
    }

    private async Task ClearExistingDefaultsAsync(DateTimeOffset now, CancellationToken ct)
    {
        foreach (var existing in await _registries.ListDefaultsAsync(ct).ConfigureAwait(false))
            existing.SetDefault(false, now);
    }
}

// ---- Update -------------------------------------------------------------------------------------

public sealed record UpdateRegistryCommand(
    Guid RegistryId,
    RegistryProviderDto Provider,
    string RegistryHost,
    string RepositoryPath,
    RegistryAuthMethodDto AuthMethod,
    string? Username,
    string? CredentialSecretRef);

public sealed class UpdateRegistryValidator : AbstractValidator<UpdateRegistryCommand>
{
    public UpdateRegistryValidator()
    {
        RuleFor(x => x.RegistryId).NotEmpty();
        RuleFor(x => x.RegistryHost).NotEmpty().MaximumLength(300);
        RuleFor(x => x.RepositoryPath).MaximumLength(500);
    }
}

public sealed class UpdateRegistryHandler
{
    private readonly IRemoteRegistryRepository _registries;
    private readonly IUnitOfWork _uow;
    private readonly TimeProvider _clock;

    public UpdateRegistryHandler(IRemoteRegistryRepository registries, IUnitOfWork uow, TimeProvider clock)
    {
        _registries = registries;
        _uow = uow;
        _clock = clock;
    }

    public async Task HandleAsync(UpdateRegistryCommand cmd, CancellationToken cancellationToken = default)
    {
        var registry = await _registries.GetByIdAsync(cmd.RegistryId, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Registry {cmd.RegistryId} not found.");

        registry.UpdateConfiguration(
            cmd.Provider.ToDomain(), cmd.RegistryHost, cmd.RepositoryPath,
            cmd.AuthMethod.ToDomain(), cmd.Username, cmd.CredentialSecretRef, _clock.GetUtcNow());

        await _uow.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}

// ---- Activation / Default / Delete --------------------------------------------------------------

public sealed record ChangeRegistryActivationCommand(Guid RegistryId, bool Enabled);

public sealed class ChangeRegistryActivationHandler
{
    private readonly IRemoteRegistryRepository _registries;
    private readonly IUnitOfWork _uow;
    private readonly TimeProvider _clock;

    public ChangeRegistryActivationHandler(IRemoteRegistryRepository registries, IUnitOfWork uow, TimeProvider clock)
    {
        _registries = registries;
        _uow = uow;
        _clock = clock;
    }

    public async Task HandleAsync(ChangeRegistryActivationCommand cmd, CancellationToken cancellationToken = default)
    {
        var registry = await _registries.GetByIdAsync(cmd.RegistryId, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Registry {cmd.RegistryId} not found.");
        registry.ChangeActivation(cmd.Enabled, _clock.GetUtcNow());
        await _uow.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}

public sealed record SetDefaultRegistryCommand(Guid RegistryId);

public sealed class SetDefaultRegistryHandler
{
    private readonly IRemoteRegistryRepository _registries;
    private readonly IUnitOfWork _uow;
    private readonly TimeProvider _clock;

    public SetDefaultRegistryHandler(IRemoteRegistryRepository registries, IUnitOfWork uow, TimeProvider clock)
    {
        _registries = registries;
        _uow = uow;
        _clock = clock;
    }

    public async Task HandleAsync(SetDefaultRegistryCommand cmd, CancellationToken cancellationToken = default)
    {
        var registry = await _registries.GetByIdAsync(cmd.RegistryId, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Registry {cmd.RegistryId} not found.");

        var now = _clock.GetUtcNow();
        foreach (var existing in await _registries.ListDefaultsAsync(cancellationToken).ConfigureAwait(false))
            if (existing.Id != registry.Id) existing.SetDefault(false, now);

        registry.SetDefault(true, now);
        await _uow.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}

public sealed record DeleteRegistryCommand(Guid RegistryId);

public sealed class DeleteRegistryHandler
{
    private readonly IRemoteRegistryRepository _registries;
    private readonly IAutomationRuleRepository _rules;
    private readonly IUnitOfWork _uow;

    public DeleteRegistryHandler(IRemoteRegistryRepository registries, IAutomationRuleRepository rules, IUnitOfWork uow)
    {
        _registries = registries;
        _rules = rules;
        _uow = uow;
    }

    public async Task HandleAsync(DeleteRegistryCommand cmd, CancellationToken cancellationToken = default)
    {
        var registry = await _registries.GetByIdAsync(cmd.RegistryId, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Registry {cmd.RegistryId} not found.");

        var referencing = await _rules.ListByRegistryAsync(registry.Id, cancellationToken).ConfigureAwait(false);
        if (referencing.Count > 0)
            throw new InvalidOperationException(
                $"Registry '{registry.Name}' is referenced by {referencing.Count} rule(s); delete or retarget them first.");

        _registries.Remove(registry);
        await _uow.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
