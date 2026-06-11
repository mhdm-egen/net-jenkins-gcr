namespace Deployment.Domain.Releases;

/// <summary>
/// Shape of the artifact a release points to. Application releases are
/// manifest-only — they have no artifact and use this enum's <see cref="Manifest"/>
/// value with a null <c>ArtifactUri</c>.
/// </summary>
public enum ArtifactType
{
    /// <summary>Application release: pure BOM, no artifact (ArtifactUri must be null).</summary>
    Manifest = 0,
    Zip = 1,
    ContainerImage = 2,
    NuGet = 3,
}
