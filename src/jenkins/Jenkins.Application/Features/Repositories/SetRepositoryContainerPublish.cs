using Jenkins.Contracts.Repositories;
using Jenkins.Domain.Abstractions;
using Jenkins.Domain.SourceRepositories;
using FluentValidation;

namespace Jenkins.Application.Features.Repositories;

/// <summary>
/// Allow or suppress container production for a repository (the per-repo half of the
/// containerizable gate; combines with the code-level <c>Containerizable</c> opt-in).
/// Idempotent — the aggregate no-ops when already in the requested state.
/// </summary>
public sealed record SetRepositoryContainerPublishCommand(Guid Id, bool AllowContainerPublish);

public sealed class SetRepositoryContainerPublishValidator : AbstractValidator<SetRepositoryContainerPublishCommand>
{
    public SetRepositoryContainerPublishValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
    }
}

public sealed class SetRepositoryContainerPublishHandler
{
    private readonly ISourceRepositoryStore _repositories;
    private readonly IUnitOfWork _uow;
    private readonly TimeProvider _clock;

    public SetRepositoryContainerPublishHandler(ISourceRepositoryStore repositories, IUnitOfWork uow, TimeProvider clock)
    {
        _repositories = repositories;
        _uow = uow;
        _clock = clock;
    }

    public async Task<RepositoryDto> HandleAsync(SetRepositoryContainerPublishCommand cmd, CancellationToken cancellationToken = default)
    {
        var repository = await _repositories.GetByIdAsync(cmd.Id, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Repository {cmd.Id} not found.");

        repository.SetContainerPublishAllowed(cmd.AllowContainerPublish, _clock.GetUtcNow());

        await _uow.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return repository.ToDto();
    }
}
