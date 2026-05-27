namespace Jenkins.WebUI.Services.Nexus;

public interface INexusClient
{
    /// <summary>True if Url + Password were supplied at startup.</summary>
    bool IsConfigured { get; }

    /// <summary>If <see cref="IsConfigured"/> is false, the reason (missing field).</summary>
    string? ConfigurationError { get; }

    /// <summary>Repository name being enumerated (echoes <see cref="NexusOptions.NuGetHostedRepository"/>).</summary>
    string NuGetRepositoryName { get; }

    /// <summary>Repository name being enumerated (echoes <see cref="NexusOptions.DockerHostedRepository"/>).</summary>
    string DockerRepositoryName { get; }

    /// <summary>
    /// Returns every package version in the configured nuget-hosted repository.
    /// Walks Nexus's continuation-token paging until exhausted.
    /// </summary>
    Task<IReadOnlyList<NuGetPackage>> ListNuGetPackagesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns every image+tag in the configured docker-hosted repository.
    /// Walks Nexus's continuation-token paging until exhausted.
    /// </summary>
    Task<IReadOnlyList<DockerImage>> ListDockerImagesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a single Nexus component (one package version, all its assets).
    /// </summary>
    Task DeleteNuGetPackageAsync(NuGetPackage package, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a single Nexus component (one image+tag, all its associated assets).
    /// Shared blob layers used by other tags are retained by Nexus.
    /// </summary>
    Task DeleteDockerImageAsync(DockerImage image, CancellationToken cancellationToken = default);
}
