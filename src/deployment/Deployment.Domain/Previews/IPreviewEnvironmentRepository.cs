using Deployment.Domain.Abstractions;

namespace Deployment.Domain.Previews;

public interface IPreviewEnvironmentRepository : IRepository<PreviewEnvironment, Guid>
{
    /// <summary>The most recent non-torn-down preview for an app + key, used to keep create idempotent.</summary>
    Task<PreviewEnvironment?> FindLiveByAppAndKeyAsync(Guid applicationId, string key, CancellationToken cancellationToken = default);

    /// <summary>Active previews whose TTL has elapsed — the sweeper tears these down.</summary>
    Task<IReadOnlyList<PreviewEnvironment>> ListExpiredActiveAsync(DateTimeOffset now, CancellationToken cancellationToken = default);
}
