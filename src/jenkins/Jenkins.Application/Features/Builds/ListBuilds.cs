using Jenkins.Contracts.Builds;
using Jenkins.Domain.Builds;

namespace Jenkins.Application.Features.Builds;

/// <summary>
/// Read-model port for the build list (flat summaries). Build detail loads the
/// aggregate via <see cref="IBuildStore"/> and maps in memory.
/// </summary>
public interface IBuildCatalogReader
{
    Task<IReadOnlyList<BuildSummaryDto>> ListByRepositoryAsync(Guid repositoryId, int take, CancellationToken cancellationToken = default);
}

public sealed record ListBuildsQuery(Guid RepositoryId, int Take);

public sealed class ListBuildsHandler
{
    private readonly IBuildCatalogReader _reader;
    public ListBuildsHandler(IBuildCatalogReader reader) => _reader = reader;

    public Task<IReadOnlyList<BuildSummaryDto>> HandleAsync(ListBuildsQuery query, CancellationToken cancellationToken = default)
        => _reader.ListByRepositoryAsync(query.RepositoryId, query.Take, cancellationToken);
}

public sealed record GetBuildByIdQuery(Guid Id);

public sealed class GetBuildByIdHandler
{
    private readonly IBuildStore _builds;
    public GetBuildByIdHandler(IBuildStore builds) => _builds = builds;

    public async Task<BuildDetailDto?> HandleAsync(GetBuildByIdQuery query, CancellationToken cancellationToken = default)
    {
        var build = await _builds.GetByIdAsync(query.Id, cancellationToken).ConfigureAwait(false);
        return build?.ToDetailDto();
    }
}
