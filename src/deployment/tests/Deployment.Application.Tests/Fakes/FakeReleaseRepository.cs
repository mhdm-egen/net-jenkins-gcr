using Deployment.Domain.Releases;

namespace Deployment.Application.Tests.Fakes;

internal sealed class FakeReleaseRepository : IReleaseRepository
{
    private readonly Dictionary<Guid, Release> _byId = new();
    public IReadOnlyDictionary<Guid, Release> All => _byId;

    public void Seed(Release r) => _byId[r.Id] = r;

    public Task<Release?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => Task.FromResult(_byId.GetValueOrDefault(id));

    public Task AddAsync(Release aggregate, CancellationToken cancellationToken = default)
    {
        _byId[aggregate.Id] = aggregate;
        return Task.CompletedTask;
    }

    public void Remove(Release aggregate) => _byId.Remove(aggregate.Id);

    public Task<Release?> FindLatestAvailableAsync(Guid deployableUnitId, CancellationToken ct = default)
    {
        var hit = _byId.Values
            .Where(r => r.DeployableUnitId == deployableUnitId && r.Status == ReleaseStatus.Available)
            .OrderByDescending(r => r.CreatedAtUtc)
            .FirstOrDefault();
        return Task.FromResult<Release?>(hit);
    }

    public Task<Release?> FindByVersionAsync(Guid deployableUnitId, string semanticVersion, CancellationToken ct = default)
    {
        var hit = _byId.Values.FirstOrDefault(r =>
            r.DeployableUnitId == deployableUnitId && r.SemanticVersion == semanticVersion);
        return Task.FromResult<Release?>(hit);
    }
}
