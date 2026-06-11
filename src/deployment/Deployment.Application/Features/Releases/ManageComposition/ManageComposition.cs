using Deployment.Contracts.Releases;
using Deployment.Domain.Abstractions;
using Deployment.Domain.Releases;
using FluentValidation;

namespace Deployment.Application.Features.Releases.ManageComposition;

// --- Add ---

public sealed record AddCompositionEntryCommand(
    Guid ReleaseId,
    Guid ServiceId,
    PinModeDto PinMode,
    Guid? ServiceReleaseId);

public sealed class AddCompositionEntryValidator : AbstractValidator<AddCompositionEntryCommand>
{
    public AddCompositionEntryValidator()
    {
        RuleFor(x => x.ReleaseId).NotEmpty();
        RuleFor(x => x.ServiceId).NotEmpty();

        // Domain enforces the pin invariant too, but catching it at the boundary
        // gives a cleaner 400 instead of a 409.
        RuleFor(x => x.ServiceReleaseId)
            .NotNull().NotEqual(Guid.Empty)
            .When(x => x.PinMode == PinModeDto.Pinned)
            .WithMessage("PinMode=Pinned requires a non-empty ServiceReleaseId.");
        RuleFor(x => x.ServiceReleaseId)
            .Null()
            .When(x => x.PinMode != PinModeDto.Pinned)
            .WithMessage("PinMode Latest/Current require ServiceReleaseId to be null.");
    }
}

public sealed class AddCompositionEntryHandler
{
    private readonly IReleaseRepository _releases;
    private readonly IUnitOfWork _uow;
    private readonly TimeProvider _clock;

    public AddCompositionEntryHandler(IReleaseRepository releases, IUnitOfWork uow, TimeProvider clock)
    {
        _releases = releases;
        _uow = uow;
        _clock = clock;
    }

    public async Task HandleAsync(AddCompositionEntryCommand cmd, CancellationToken cancellationToken = default)
    {
        var release = await _releases.GetByIdAsync(cmd.ReleaseId, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Release {cmd.ReleaseId} not found.");

        release.AddComposition(cmd.ServiceId, ReleaseMapping.ToDomain(cmd.PinMode),
            cmd.ServiceReleaseId, _clock.GetUtcNow());
        await _uow.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}

// --- Update ---

public sealed record UpdateCompositionEntryCommand(
    Guid ReleaseId,
    Guid ServiceId,
    PinModeDto PinMode,
    Guid? ServiceReleaseId);

public sealed class UpdateCompositionEntryValidator : AbstractValidator<UpdateCompositionEntryCommand>
{
    public UpdateCompositionEntryValidator()
    {
        RuleFor(x => x.ReleaseId).NotEmpty();
        RuleFor(x => x.ServiceId).NotEmpty();
        RuleFor(x => x.ServiceReleaseId)
            .NotNull().NotEqual(Guid.Empty)
            .When(x => x.PinMode == PinModeDto.Pinned)
            .WithMessage("PinMode=Pinned requires a non-empty ServiceReleaseId.");
        RuleFor(x => x.ServiceReleaseId)
            .Null()
            .When(x => x.PinMode != PinModeDto.Pinned)
            .WithMessage("PinMode Latest/Current require ServiceReleaseId to be null.");
    }
}

public sealed class UpdateCompositionEntryHandler
{
    private readonly IReleaseRepository _releases;
    private readonly IUnitOfWork _uow;
    private readonly TimeProvider _clock;

    public UpdateCompositionEntryHandler(IReleaseRepository releases, IUnitOfWork uow, TimeProvider clock)
    {
        _releases = releases;
        _uow = uow;
        _clock = clock;
    }

    public async Task HandleAsync(UpdateCompositionEntryCommand cmd, CancellationToken cancellationToken = default)
    {
        var release = await _releases.GetByIdAsync(cmd.ReleaseId, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Release {cmd.ReleaseId} not found.");

        release.UpdateComposition(cmd.ServiceId, ReleaseMapping.ToDomain(cmd.PinMode),
            cmd.ServiceReleaseId, _clock.GetUtcNow());
        await _uow.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}

// --- Remove ---

public sealed record RemoveCompositionEntryCommand(Guid ReleaseId, Guid ServiceId);

public sealed class RemoveCompositionEntryValidator : AbstractValidator<RemoveCompositionEntryCommand>
{
    public RemoveCompositionEntryValidator()
    {
        RuleFor(x => x.ReleaseId).NotEmpty();
        RuleFor(x => x.ServiceId).NotEmpty();
    }
}

public sealed class RemoveCompositionEntryHandler
{
    private readonly IReleaseRepository _releases;
    private readonly IUnitOfWork _uow;
    private readonly TimeProvider _clock;

    public RemoveCompositionEntryHandler(IReleaseRepository releases, IUnitOfWork uow, TimeProvider clock)
    {
        _releases = releases;
        _uow = uow;
        _clock = clock;
    }

    public async Task HandleAsync(RemoveCompositionEntryCommand cmd, CancellationToken cancellationToken = default)
    {
        var release = await _releases.GetByIdAsync(cmd.ReleaseId, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Release {cmd.ReleaseId} not found.");

        release.RemoveComposition(cmd.ServiceId, _clock.GetUtcNow());
        await _uow.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
