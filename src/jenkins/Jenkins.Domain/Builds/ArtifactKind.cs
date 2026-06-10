namespace Jenkins.Domain.Builds;

/// <summary>
/// What a build produced. The point where "assemblies vs containers" diverge:
/// a <see cref="NuGetPackage"/> is pushed to Nexus NuGet; a
/// <see cref="ContainerImage"/> is pushed to Nexus Docker (and later promoted to
/// GAR by the deployment service — never by CI).
/// </summary>
public enum ArtifactKind
{
    NuGetPackage = 0,
    ContainerImage = 1,
}
