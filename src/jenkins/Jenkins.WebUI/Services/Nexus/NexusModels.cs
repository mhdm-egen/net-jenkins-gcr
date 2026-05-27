namespace Jenkins.WebUI.Services.Nexus;

/// <summary>
/// One package version stored in a Nexus hosted NuGet repository. Each Nexus
/// "component" corresponds to a single package+version; the .nupkg / .nuspec
/// assets share the same uploaded-at timestamp so we collapse onto the first.
/// </summary>
public sealed record NuGetPackage(
    string ComponentId,                 // Nexus internal component id; required for DELETE
    string Name,
    string Version,
    long? SizeBytes,
    DateTimeOffset? UploadedAt,
    string? DownloadUrl,
    string? Sha256);

/// <summary>
/// One image+tag in a Nexus hosted Docker repository. <see cref="SizeBytes"/> is the
/// sum of the component's asset file sizes — in practice mostly the manifest;
/// shared blob layers may live as separate components and are NOT counted here.
/// </summary>
public sealed record DockerImage(
    string ComponentId,
    string Name,                        // e.g. "egen/web"
    string Tag,                         // e.g. "v1", "latest"
    long? SizeBytes,
    DateTimeOffset? UploadedAt);
