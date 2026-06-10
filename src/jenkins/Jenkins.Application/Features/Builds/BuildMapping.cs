using Jenkins.Contracts.Builds;
using Jenkins.Domain.Builds;

namespace Jenkins.Application.Features.Builds;

/// <summary>
/// In-memory projection of the <see cref="Build"/> aggregate (and its children) to
/// wire DTOs. Detail/artifact mapping runs in memory off a loaded aggregate to
/// avoid translating the nested collections + tags column in SQL.
/// </summary>
internal static class BuildMapping
{
    public static BuildSummaryDto ToSummaryDto(this Build b) => new(
        Id: b.Id,
        RepositoryId: b.RepositoryId,
        CiJobName: b.CiJobName,
        CiBuildNumber: b.CiBuildNumber,
        CiRunUrl: b.CiRunUrl,
        CommitShort: b.SourceRevision.CommitShort,
        Branch: b.SourceRevision.Branch,
        PackageVersion: b.Versions?.PackageVersion,
        Status: (BuildStatusDto)(int)b.Status,
        StartedAtUtc: b.StartedAtUtc,
        CompletedAtUtc: b.CompletedAtUtc);

    public static BuildDetailDto ToDetailDto(this Build b) => new(
        Head: b.ToSummaryDto(),
        FileVersion: b.Versions?.FileVersion,
        AssemblyVersion: b.Versions?.AssemblyVersion,
        InformationalVersion: b.Versions?.InformationalVersion,
        BaseVersion: b.Versions?.BaseVersion,
        SbomUri: b.Quality?.SbomUri,
        VulnerabilityReportUri: b.Quality?.VulnerabilityReportUri,
        TriggeredBy: b.TriggeredBy,
        DurationMs: b.DurationMs,
        Artifacts: b.Artifacts.OrderBy(a => a.Name).Select(a => a.ToDto()).ToList());

    public static BuildArtifactDto ToDto(this BuildArtifact a) => new(
        Id: a.Id,
        Kind: (ArtifactKindDto)(int)a.Kind,
        Name: a.Name,
        Version: a.Version,
        Digest: a.Digest,
        SizeBytes: a.SizeBytes,
        Publications: a.Publications.Select(p => p.ToDto()).ToList());

    public static ArtifactPublicationDto ToDto(this ArtifactPublication p) => new(
        Id: p.Id,
        Registry: (PublicationRegistryDto)(int)p.Registry,
        Reference: p.Reference,
        Tags: p.Tags.ToList(),
        PublishedAtUtc: p.PublishedAtUtc);
}
