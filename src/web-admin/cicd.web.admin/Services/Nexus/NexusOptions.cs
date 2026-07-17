namespace Cicd.Web.Admin.Services.Nexus;

public sealed record NexusOptions
{
    /// <summary>Base URL of the Nexus server (e.g. http://nexus:8081). Trailing slash trimmed.</summary>
    public string Url { get; init; } = string.Empty;

    public string User { get; init; } = "admin";

    /// <summary>
    /// Basic-auth password (or token). NEVER commit this — supply via env var
    /// <c>Nexus__Password</c> (double underscore) or docker-compose secret.
    /// </summary>
    public string Password { get; init; } = string.Empty;

    /// <summary>Name of the hosted NuGet repository to enumerate (Nexus repo name, not URL).</summary>
    public string NuGetHostedRepository { get; init; } = "nuget-hosted";

    /// <summary>Name of the hosted Docker repository to enumerate (Nexus repo name, not URL).</summary>
    public string DockerHostedRepository { get; init; } = "docker-hosted";

    /// <summary>Name of the raw repository holding SBOM/VEX artifacts (used by the reset purge).</summary>
    public string SbomRepository { get; init; } = "sboms";

    /// <summary>
    /// Host:port of the Nexus docker registry connector (NOT the :8081 REST API) — used to build
    /// the pull reference (<c>host/name:tag</c>) when manually adding a container to the publisher
    /// inventory. Matches the registry the build pipeline pushes to.
    /// </summary>
    public string DockerRegistryHost { get; init; } = "nexus:8082";
}
