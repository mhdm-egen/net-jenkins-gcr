using Deployment.Domain.Deployments;
using DeploymentRow = Deployment.Domain.Deployments.Deployment;

namespace Deployment.Application.Tests.Fakes;

internal sealed class FakeDeploymentRepository : IDeploymentRepository
{
    private readonly Dictionary<Guid, DeploymentRow> _byId = new();

    // Caller supplies the Release lookup (releaseId → deployableUnitId) so this
    // fake can answer FindLatestSucceeded without owning a Release fake too.
    public Func<Guid, Guid> ReleaseToUnit { get; set; } = _ => Guid.Empty;

    public void Seed(DeploymentRow d) => _byId[d.Id] = d;

    public Task<DeploymentRow?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => Task.FromResult(_byId.GetValueOrDefault(id));

    public Task AddAsync(DeploymentRow aggregate, CancellationToken ct = default)
    {
        _byId[aggregate.Id] = aggregate;
        return Task.CompletedTask;
    }

    public void Remove(DeploymentRow aggregate) => _byId.Remove(aggregate.Id);

    public Task<DeploymentRow?> FindLatestSucceededAsync(
        Guid deployableUnitId, Guid environmentId, CancellationToken ct = default)
    {
        var hit = _byId.Values
            .Where(d => d.EnvironmentId == environmentId
                        && d.Status == DeploymentStatus.Succeeded
                        && ReleaseToUnit(d.ReleaseId) == deployableUnitId)
            .OrderByDescending(d => d.CompletedAtUtc)
            .FirstOrDefault();
        return Task.FromResult<DeploymentRow?>(hit);
    }

    public Task<IReadOnlyList<DeploymentRow>> GetCascadeAsync(Guid parentDeploymentId, CancellationToken ct = default)
    {
        var list = _byId.Values
            .Where(d => d.Id == parentDeploymentId || d.ParentDeploymentId == parentDeploymentId)
            .ToList();
        return Task.FromResult<IReadOnlyList<DeploymentRow>>(list);
    }
}
