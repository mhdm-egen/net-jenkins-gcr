using FluentValidation;
using Deployment.Contracts.AspireApps;
using Deployment.Domain.Abstractions;
using Deployment.Domain.AspireApps;

namespace Deployment.Application.Features.AspireApps;

internal static class AspireAppMapping
{
    public static AspireApplicationDto ToDto(this AspireApplication a) =>
        new(a.Id, a.Name, a.Description, a.AppHostPath, a.KubeContext, a.Namespace, a.IsActive, a.CreatedAtUtc, a.UpdatedAtUtc);
}

public sealed record CreateAspireApplicationCommand(string Name, string? Description, string AppHostPath, string KubeContext, string Namespace);

public sealed class CreateAspireApplicationValidator : AbstractValidator<CreateAspireApplicationCommand>
{
    public CreateAspireApplicationValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.AppHostPath).NotEmpty().MaximumLength(1000);
        RuleFor(x => x.KubeContext).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Namespace).NotEmpty().MaximumLength(200);
    }
}

public sealed class CreateAspireApplicationHandler
{
    private readonly IAspireApplicationRepository _apps;
    private readonly IUnitOfWork _uow;
    private readonly TimeProvider _clock;
    public CreateAspireApplicationHandler(IAspireApplicationRepository apps, IUnitOfWork uow, TimeProvider clock)
    { _apps = apps; _uow = uow; _clock = clock; }

    public async Task<AspireApplicationDto> HandleAsync(CreateAspireApplicationCommand cmd, CancellationToken ct = default)
    {
        if (await _apps.FindByNameAsync(cmd.Name, ct).ConfigureAwait(false) is not null)
            throw new InvalidOperationException($"An Aspire application named '{cmd.Name}' already exists.");
        var app = new AspireApplication(Guid.NewGuid(), cmd.Name, cmd.Description, cmd.AppHostPath, cmd.KubeContext, cmd.Namespace, _clock.GetUtcNow());
        await _apps.AddAsync(app, ct).ConfigureAwait(false);
        await _uow.SaveChangesAsync(ct).ConfigureAwait(false);
        return app.ToDto();
    }
}

public sealed record UpdateAspireApplicationCommand(Guid ApplicationId, string Name, string? Description, string AppHostPath, string KubeContext, string Namespace);

public sealed class UpdateAspireApplicationValidator : AbstractValidator<UpdateAspireApplicationCommand>
{
    public UpdateAspireApplicationValidator()
    {
        RuleFor(x => x.ApplicationId).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.AppHostPath).NotEmpty().MaximumLength(1000);
        RuleFor(x => x.KubeContext).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Namespace).NotEmpty().MaximumLength(200);
    }
}

public sealed class UpdateAspireApplicationHandler
{
    private readonly IAspireApplicationRepository _apps;
    private readonly IUnitOfWork _uow;
    private readonly TimeProvider _clock;
    public UpdateAspireApplicationHandler(IAspireApplicationRepository apps, IUnitOfWork uow, TimeProvider clock)
    { _apps = apps; _uow = uow; _clock = clock; }

    public async Task HandleAsync(UpdateAspireApplicationCommand cmd, CancellationToken ct = default)
    {
        var app = await _apps.GetByIdAsync(cmd.ApplicationId, ct).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Aspire application {cmd.ApplicationId} not found.");
        app.Update(cmd.Name, cmd.Description, cmd.AppHostPath, cmd.KubeContext, cmd.Namespace, _clock.GetUtcNow());
        await _uow.SaveChangesAsync(ct).ConfigureAwait(false);
    }
}

public sealed record DeleteAspireApplicationCommand(Guid ApplicationId);

public sealed class DeleteAspireApplicationHandler
{
    private readonly IAspireApplicationRepository _apps;
    private readonly IUnitOfWork _uow;
    public DeleteAspireApplicationHandler(IAspireApplicationRepository apps, IUnitOfWork uow)
    { _apps = apps; _uow = uow; }

    public async Task HandleAsync(DeleteAspireApplicationCommand cmd, CancellationToken ct = default)
    {
        var app = await _apps.GetByIdAsync(cmd.ApplicationId, ct).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Aspire application {cmd.ApplicationId} not found.");
        _apps.Remove(app);
        await _uow.SaveChangesAsync(ct).ConfigureAwait(false);
    }
}
