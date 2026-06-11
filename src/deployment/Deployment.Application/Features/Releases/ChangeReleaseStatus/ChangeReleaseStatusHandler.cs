using Deployment.Domain.Abstractions;
using Deployment.Domain.Releases;

namespace Deployment.Application.Features.Releases.ChangeReleaseStatus;

public sealed class ChangeReleaseStatusHandler
{
    private readonly IReleaseRepository _releases;
    private readonly IUnitOfWork _uow;
    private readonly TimeProvider _clock;

    public ChangeReleaseStatusHandler(
        IReleaseRepository releases,
        IUnitOfWork uow,
        TimeProvider clock)
    {
        _releases = releases;
        _uow = uow;
        _clock = clock;
    }

    public async Task HandleAsync(ChangeReleaseStatusCommand cmd, CancellationToken cancellationToken = default)
    {
        var release = await _releases.GetByIdAsync(cmd.ReleaseId, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Release {cmd.ReleaseId} not found.");

        release.ChangeStatus(ReleaseMapping.ToDomain(cmd.NewStatus), cmd.Reason, cmd.ChangedByPrincipal, _clock.GetUtcNow());

        await _uow.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
