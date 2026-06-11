namespace Deployment.Application.Abstractions;

/// <summary>
/// Resolves a container coordinate + tag to an immutable, pull-by-digest reference by
/// querying the source registry (Nexus is the system of record). Implemented in
/// Infrastructure. Decision #2/#3: tags are a selector resolved to a digest at authoring
/// time; the available tags are discovered live, never frozen on the coordinate.
/// See <c>docs/deployment/container-image-source.md</c>.
/// </summary>
public interface IContainerImageResolver
{
    /// <summary>
    /// Resolve <c>{registry}/{repository}/{name}:{tag}</c> to its digest reference
    /// <c>{registry}/{repository}/{name}@sha256:&lt;digest&gt;</c>. Returns <c>null</c> when
    /// the tag does not exist in the registry.
    /// </summary>
    Task<ContainerImageResolution?> ResolveAsync(
        string registry, string repository, string name, string tag,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// List the tags currently available for the coordinate (release-modal discovery).
    /// Ordered most-recent-first where the registry exposes ordering, else lexical.
    /// </summary>
    Task<IReadOnlyList<string>> ListTagsAsync(
        string registry, string repository, string name,
        CancellationToken cancellationToken = default);
}

/// <summary>The outcome of resolving a tag: the bare digest and the full pull-by-digest ref.</summary>
/// <param name="Digest">The content digest, e.g. <c>sha256:abc…</c>.</param>
/// <param name="DigestRef">The full reference, <c>{registry}/{repository}/{name}@{Digest}</c>.</param>
public sealed record ContainerImageResolution(string Digest, string DigestRef);
