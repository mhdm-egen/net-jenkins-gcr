namespace Deployment.Infrastructure.Nexus;

/// <summary>
/// How the deployment service reaches the Nexus docker registry's v2 API to resolve image digests
/// (for digest-pinning Aspire deploys). Bound from <c>Deployment:Nexus</c>. The URL is whatever the
/// SERVICE can reach (e.g. <c>http://localhost:8082</c>); the resolved digest is host-independent, so
/// it pins correctly regardless of the registry host the cluster pulls from.
/// </summary>
public sealed class NexusRegistryOptions
{
    public const string SectionName = "Deployment:Nexus";

    public string RegistryV2Url { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;

    public bool Enabled => !string.IsNullOrWhiteSpace(RegistryV2Url);

    /// <summary>
    /// Host[:port] this service should DIAL for the Nexus docker registry, replacing whatever host
    /// was recorded in an image reference. Empty (the default) = use the recorded reference
    /// unchanged, which is correct whenever CI and this service share a view of the registry.
    ///
    /// Why this exists: <c>Nexus:DockerRegistryHost</c> is a RECORDED pull reference — jenkins-api
    /// bakes it into <c>{host}/{name}@{digest}</c>, which becomes <c>KnownContainer.NexusRef</c> and
    /// then a deploy run's <c>SourceRef</c>. It is deliberately set to the value BUILD AGENTS and
    /// CLUSTER NODES need (<c>nexus:8082</c>), and the AppHost documents it as "not dialled by the
    /// Aspire-run host processes".
    ///
    /// That holds everywhere except one hop: GarPush hands that same string to <c>crane copy</c>,
    /// which runs in THIS process — and a host process cannot resolve a compose/Aspire container
    /// hostname, so the copy dies with "lookup nexus: no such host". One value was serving two
    /// purposes with different requirements.
    ///
    /// Only the HOST is replaced; the repository path and the digest/tag are preserved, so this
    /// cannot repoint a deploy at a different image. This is the image-reference counterpart of
    /// <c>Deployment:Aspirate:ManifestBaseUrl</c>, which solves exactly this problem for manifests.
    /// </summary>
    public string DockerRegistryDialHost { get; set; } = string.Empty;

    /// <summary>
    /// Applies <see cref="DockerRegistryDialHost"/> to a recorded image reference.
    ///
    /// Leaves the reference alone when no dial host is configured, and when the recorded reference
    /// carries no explicit registry host — a bare <c>name:tag</c> means Docker Hub, and injecting a
    /// host there would change which image is pulled rather than merely how it is reached.
    /// </summary>
    public string ToDialableRef(string imageRef)
    {
        if (string.IsNullOrWhiteSpace(DockerRegistryDialHost) || string.IsNullOrWhiteSpace(imageRef))
            return imageRef;

        var slash = imageRef.IndexOf('/');
        if (slash <= 0) return imageRef;

        // A first segment is a registry host only if it looks like one: contains a dot or a port, or
        // is literally localhost. Otherwise it is a Docker Hub namespace (e.g. "library/nginx").
        var host = imageRef[..slash];
        var looksLikeHost = host.Contains('.') || host.Contains(':')
                            || host.Equals("localhost", StringComparison.OrdinalIgnoreCase);

        return looksLikeHost
            ? DockerRegistryDialHost.Trim().TrimEnd('/') + imageRef[slash..]
            : imageRef;
    }
}
