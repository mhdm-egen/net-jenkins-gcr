using Jenkins.Domain.Common;

namespace Jenkins.Domain.Builds;

/// <summary>
/// The resolved version block computed once in <c>cicd-build</c> and carried in
/// <c>build-info.json</c>. <see cref="PackageVersion"/> is what the deployment
/// service receives as the Release SemanticVersion (CI decision #4) — it is
/// unique per build (embeds build number + commit short).
/// </summary>
public sealed class BuildVersions : ValueObject
{
    /// <summary>e.g. <c>1.0.0-ci.42.g7a4b9c1</c> — the NuGet/handoff version.</summary>
    public string PackageVersion { get; }

    /// <summary>e.g. <c>1.0.0.42</c> — assembly file version.</summary>
    public string FileVersion { get; }

    /// <summary>e.g. <c>1.0.0.0</c> — assembly version.</summary>
    public string AssemblyVersion { get; }

    /// <summary>e.g. <c>1.0.0-ci.42.g7a4b9c1+7a4b9c1</c> — informational version.</summary>
    public string InformationalVersion { get; }

    /// <summary>The <c>BASE_VER</c> the rest are derived from (e.g. <c>1.0.0</c>).</summary>
    public string BaseVersion { get; }

    public BuildVersions(
        string packageVersion,
        string fileVersion,
        string assemblyVersion,
        string informationalVersion,
        string baseVersion)
    {
        if (string.IsNullOrWhiteSpace(packageVersion))
            throw new ArgumentException("PackageVersion cannot be empty.", nameof(packageVersion));
        if (string.IsNullOrWhiteSpace(fileVersion))
            throw new ArgumentException("FileVersion cannot be empty.", nameof(fileVersion));
        if (string.IsNullOrWhiteSpace(assemblyVersion))
            throw new ArgumentException("AssemblyVersion cannot be empty.", nameof(assemblyVersion));
        if (string.IsNullOrWhiteSpace(informationalVersion))
            throw new ArgumentException("InformationalVersion cannot be empty.", nameof(informationalVersion));
        if (string.IsNullOrWhiteSpace(baseVersion))
            throw new ArgumentException("BaseVersion cannot be empty.", nameof(baseVersion));

        PackageVersion = packageVersion.Trim();
        FileVersion = fileVersion.Trim();
        AssemblyVersion = assemblyVersion.Trim();
        InformationalVersion = informationalVersion.Trim();
        BaseVersion = baseVersion.Trim();
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return PackageVersion;
        yield return FileVersion;
        yield return AssemblyVersion;
        yield return InformationalVersion;
        yield return BaseVersion;
    }
}
