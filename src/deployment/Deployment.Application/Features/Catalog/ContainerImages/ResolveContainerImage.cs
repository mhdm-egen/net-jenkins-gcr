using Deployment.Application.Abstractions;
using Deployment.Contracts.Catalog;

namespace Deployment.Application.Features.Catalog.ContainerImages;

/// <summary>
/// Live registry-backed queries used by the release modal (decision #2): list the tags
/// available for a coordinate, and resolve a chosen tag to an immutable digest ref.
/// </summary>
public sealed record ListContainerImageTagsQuery(string Registry, string Repository, string Name);

public sealed class ListContainerImageTagsHandler
{
    private readonly IContainerImageResolver _resolver;
    public ListContainerImageTagsHandler(IContainerImageResolver resolver) => _resolver = resolver;

    public Task<IReadOnlyList<string>> HandleAsync(ListContainerImageTagsQuery query, CancellationToken cancellationToken = default)
        => _resolver.ListTagsAsync(query.Registry, query.Repository, query.Name, cancellationToken);
}

public sealed record ResolveContainerImageQuery(string Registry, string Repository, string Name, string Tag);

public sealed class ResolveContainerImageHandler
{
    private readonly IContainerImageResolver _resolver;
    public ResolveContainerImageHandler(IContainerImageResolver resolver) => _resolver = resolver;

    public async Task<ContainerImageResolutionDto?> HandleAsync(ResolveContainerImageQuery query, CancellationToken cancellationToken = default)
    {
        var hit = await _resolver.ResolveAsync(query.Registry, query.Repository, query.Name, query.Tag, cancellationToken)
            .ConfigureAwait(false);
        return hit is null
            ? null
            : new ContainerImageResolutionDto(
                query.Registry, query.Repository, query.Name, query.Tag, hit.Digest, hit.DigestRef);
    }
}
