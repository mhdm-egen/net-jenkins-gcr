namespace Deployment.Application.Features.Catalog.ContainerImages;

/// <summary>
/// Parses a container artifact reference into a coordinate. Splits
/// <c>{registry}/{repository}/{name}[:tag|@digest]</c> into its registry, repository, and
/// (possibly multi-segment) name. Used by the release-publish auto-upsert to derive a
/// <c>ContainerImage</c> coordinate from a Release's <c>ArtifactUri</c>.
/// </summary>
internal static class ContainerImageRef
{
    public static bool TryParse(string? artifactUri, out string registry, out string repository, out string name)
    {
        registry = repository = name = string.Empty;
        if (string.IsNullOrWhiteSpace(artifactUri)) return false;

        var s = artifactUri.Trim();

        // Strip the digest (@sha256:…) or the tag (the ':' after the last path segment;
        // a ':' before the first '/' is the registry port, which we keep).
        var at = s.IndexOf('@');
        if (at > 0)
        {
            s = s[..at];
        }
        else
        {
            var lastSlash = s.LastIndexOf('/');
            var tagColon = s.IndexOf(':', lastSlash + 1);
            if (tagColon > 0) s = s[..tagColon];
        }

        var parts = s.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 3) return false; // need registry / repository / name

        registry = parts[0];
        repository = parts[1];
        name = string.Join('/', parts[2..]);
        return registry.Length > 0 && repository.Length > 0 && name.Length > 0;
    }
}
