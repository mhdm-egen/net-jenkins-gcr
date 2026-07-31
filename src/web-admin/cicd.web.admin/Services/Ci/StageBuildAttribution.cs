using System.Text.RegularExpressions;
using Jenkins.Client;
using Jenkins.Contracts.Builds;

namespace Cicd.Web.Admin.Services.Ci;

/// <summary>
/// Works out which repository a *stage* build (scan / publish / promote) belongs to.
///
/// The build catalog only ingests each repository's own <c>CiJobName</c>
/// (<c>JenkinsBuildSyncService</c> fetches exactly one job per repo), so stage builds have to be
/// read live from Jenkins and attributed after the fact. Jenkins' own build description carries
/// everything needed, which is why this costs no extra API calls.
/// </summary>
public static class StageBuildAttribution
{
    /// <summary>
    /// The root <c>cicd-build</c> number, e.g. <c>publish cicd-scan #40 (1.0.0-ci.40.gabc1234)</c>.
    ///
    /// The job name inside the description is the build's IMMEDIATE upstream, but the number is the
    /// number of the ROOT cicd-build — <c>cicd-scan</c> re-archives the byte-identical
    /// <c>build-info.json</c> it copied from <c>cicd-build</c> (jenkins/scan/Jenkinsfile) rather than
    /// writing its own, so <c>SOURCE_BUILD_NUM = info.buildNumber</c> still reads the root number two
    /// hops down. Parse the number; never interpret the job-name token.
    /// </summary>
    private static readonly Regex RootNumberRx = new(@"#(\d+)", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>The package version in parentheses, if the job put one there.</summary>
    private static readonly Regex VersionRx = new(@"\(([^)]+)\)", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>
    /// PACKAGE_VERSION is <c>{BASE_VER}-ci.{BUILD_NUMBER}.g{GIT_COMMIT_SHORT}</c>
    /// (jenkins/build/Jenkinsfile), so the version alone yields both the build number and the commit.
    /// Applied to the whole description, not just the parenthesised part, so jobs that print a bare
    /// version (cicd-aspire-publish) still give up a commit sha.
    /// </summary>
    private static readonly Regex VersionPartsRx =
        new(@"-ci\.(\d+)\.g([0-9a-fA-F]{7,})", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public readonly record struct ParsedDescription(int? RootBuildNumber, string? PackageVersion, string? CommitShort)
    {
        /// <summary>False when the description yielded no key at all — the build is unattributable.</summary>
        public bool HasAnyKey => RootBuildNumber is not null || CommitShort is not null;
    }

    /// <summary>
    /// Pulls the attribution keys out of a Jenkins build description. Never throws; a null or
    /// unrecognised description simply yields no keys.
    /// </summary>
    public static ParsedDescription ParseDescription(string? description)
    {
        if (string.IsNullOrWhiteSpace(description)) return default;

        var version = VersionRx.Match(description) is { Success: true } v ? v.Groups[1].Value : null;

        // Fall back to the whole string: cicd-aspire-publish writes "AppName 1.0.0-ci.7.gdeadbee"
        // with no parentheses at all.
        var parts = VersionPartsRx.Match(version ?? description);
        var commit = parts.Success ? parts.Groups[2].Value : null;

        // Prefer the explicit "#N" — it is the root build number even where the embedded one is not.
        // cicd-aspire-publish computes PACKAGE_VERSION from its OWN env.BUILD_NUMBER, so its
        // "-ci.N." is that job's number and means nothing to this repository's catalog.
        int? rootNumber = RootNumberRx.Match(description) is { Success: true } n
                          && int.TryParse(n.Groups[1].Value, out var parsed)
            ? parsed
            : null;

        if (version is null && parts.Success) version = parts.Value;

        return new ParsedDescription(rootNumber, version, commit);
    }

    public enum StageBuildOrigin
    {
        /// <summary>The description points at a build present in this repository's catalog.</summary>
        ThisRepository,

        /// <summary>The description parsed, but names a build this repository has never run.</summary>
        OtherRepository,

        /// <summary>Nothing usable in the description — do not claim it either way.</summary>
        Unknown,
    }

    public sealed record AttributedStageBuild(
        Build Build,
        StageBuildOrigin Origin,
        int? RootBuildNumber,
        string? PackageVersion);

    /// <summary>
    /// Classifies each live stage build against the repository's build catalog. Matches on the root
    /// build number first, then the commit sha — either is sufficient.
    /// </summary>
    public static IReadOnlyList<AttributedStageBuild> Attribute(
        IReadOnlyList<Build> stageBuilds,
        IReadOnlyList<BuildSummaryDto> catalog)
    {
        var numbers = catalog.Select(b => b.CiBuildNumber).ToHashSet();
        var commits = catalog.Select(b => b.CommitShort)
                             .Where(s => !string.IsNullOrWhiteSpace(s))
                             .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var result = new List<AttributedStageBuild>(stageBuilds.Count);
        foreach (var b in stageBuilds)
        {
            var p = ParseDescription(b.Description);

            var origin = !p.HasAnyKey
                ? StageBuildOrigin.Unknown
                : (p.RootBuildNumber is { } rn && numbers.Contains(rn))
                  || (p.CommitShort is { Length: > 0 } cs && commits.Contains(cs))
                    ? StageBuildOrigin.ThisRepository
                    : StageBuildOrigin.OtherRepository;

            result.Add(new AttributedStageBuild(b, origin, p.RootBuildNumber, p.PackageVersion));
        }

        return result;
    }
}
