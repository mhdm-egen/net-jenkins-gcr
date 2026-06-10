using Jenkins.Contracts.Repositories;
using Jenkins.Domain.Abstractions;
using Jenkins.Domain.SourceRepositories;
using FluentValidation;

namespace Jenkins.Application.Features.Repositories;

/// <summary>
/// Upsert a container→deployment mapping on a repo (decision #2). Matches by
/// <see cref="ContainerName"/>: remap if it already exists, otherwise add a new
/// component with <see cref="ComponentId"/>.
/// </summary>
public sealed record MapComponentCommand(
    Guid RepositoryId,
    Guid ComponentId,
    string ContainerName,
    Guid DeployableUnitId,
    string DeployableUnitName,
    bool AutoPublish);

public sealed class MapComponentValidator : AbstractValidator<MapComponentCommand>
{
    public MapComponentValidator()
    {
        RuleFor(x => x.RepositoryId).NotEmpty();
        RuleFor(x => x.ComponentId).NotEmpty();
        RuleFor(x => x.ContainerName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.DeployableUnitId).NotEmpty();
        RuleFor(x => x.DeployableUnitName).NotEmpty().MaximumLength(200);
    }
}

public sealed class MapComponentHandler
{
    private readonly ISourceRepositoryStore _repositories;
    private readonly IUnitOfWork _uow;
    private readonly TimeProvider _clock;

    public MapComponentHandler(ISourceRepositoryStore repositories, IUnitOfWork uow, TimeProvider clock)
    {
        _repositories = repositories;
        _uow = uow;
        _clock = clock;
    }

    public async Task<DeployableComponentDto> HandleAsync(MapComponentCommand cmd, CancellationToken cancellationToken = default)
    {
        var repository = await _repositories.GetByIdAsync(cmd.RepositoryId, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Repository {cmd.RepositoryId} not found.");

        var now = _clock.GetUtcNow();
        var existing = repository.Components.FirstOrDefault(c =>
            string.Equals(c.ContainerName, cmd.ContainerName.Trim(), StringComparison.OrdinalIgnoreCase));

        Guid componentId;
        if (existing is not null)
        {
            repository.RemapComponent(existing.Id, cmd.DeployableUnitId, cmd.DeployableUnitName, cmd.AutoPublish, now);
            componentId = existing.Id;
        }
        else
        {
            var added = repository.AddComponent(
                cmd.ComponentId, cmd.ContainerName, cmd.DeployableUnitId, cmd.DeployableUnitName, cmd.AutoPublish, now);
            componentId = added.Id;
        }

        await _uow.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return repository.Components.First(c => c.Id == componentId).ToDto();
    }
}
