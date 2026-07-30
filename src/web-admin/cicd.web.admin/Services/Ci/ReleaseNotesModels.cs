using System.Text;
using Jenkins.Contracts.Builds;

namespace Cicd.Web.Admin.Services.Ci;

public sealed record ReleaseNotesCommit(
    int BuildNumber,
    string CommitShort,
    string Branch,
    string? Author,
    string? Subject,
    DateTimeOffset? CommittedAtUtc,
    BuildStatusDto Status,
    string? PackageVersion);

/// <summary>
/// Grounded input for "what shipped between these two builds".
///
/// The important limitation, stated here because it has to reach the prompt: this is ONE COMMIT PER
/// BUILD — the repository head at the moment CI ran. It is not <c>git log from..to</c>. If three
/// commits land between two builds, only the third is visible anywhere in this system. Release
/// notes assembled from it are therefore a summary of what CI actually built, which is a different
/// (and smaller) thing than everything that was merged.
/// </summary>
public sealed record ReleaseNotesRequest(
    string RepositoryName,
    int FromBuild,
    int ToBuild,
    IReadOnlyList<ReleaseNotesCommit> Commits,
    int BuildsMissingCommitMetadata)
{
    /// <summary>A release range is bounded by build count, not tokens, so this cap is generous.</summary>
    public const int MaxBuilds = 100;

    public static ReleaseNotesRequest FromBuilds(
        string repositoryName, IReadOnlyList<BuildSummaryDto> builds, int fromBuild, int toBuild)
    {
        var lo = Math.Min(fromBuild, toBuild);
        var hi = Math.Max(fromBuild, toBuild);

        // Oldest-first: release notes read forwards in time even though the table is newest-first.
        var inRange = builds
            .Where(b => b.CiBuildNumber >= lo && b.CiBuildNumber <= hi)
            .OrderBy(b => b.CiBuildNumber)
            .Take(MaxBuilds)
            .ToList();

        var commits = inRange
            .Select(b => new ReleaseNotesCommit(
                BuildNumber:    b.CiBuildNumber,
                CommitShort:    b.CommitShort,
                Branch:         b.Branch,
                Author:         b.CommitAuthor,
                Subject:        b.CommitMessage,
                CommittedAtUtc: b.CommittedAtUtc,
                Status:         b.Status,
                PackageVersion: b.PackageVersion))
            .ToList();

        return new ReleaseNotesRequest(
            RepositoryName:              repositoryName,
            FromBuild:                   lo,
            ToBuild:                     hi,
            Commits:                     commits,
            BuildsMissingCommitMetadata: commits.Count(c => string.IsNullOrWhiteSpace(c.Subject)));
    }

    /// <summary>
    /// True when not one build in the range carries a commit subject. The caller uses this to
    /// refuse rather than ask the model to write release notes out of nothing but SHAs.
    /// </summary>
    public bool HasNothingToSummarize =>
        Commits.Count == 0 || BuildsMissingCommitMetadata == Commits.Count;

    public string ToPromptBlock()
    {
        var sb = new StringBuilder();

        sb.AppendLine($"Repository: {RepositoryName}");
        sb.AppendLine($"Build range: #{FromBuild} to #{ToBuild} ({Commits.Count} builds).");
        sb.AppendLine();

        sb.AppendLine("IMPORTANT — what this data is:");
        sb.AppendLine("Each row below is ONE BUILD and the single commit that was at the repository");
        sb.AppendLine("head when that build ran. This is NOT a full commit log for the range. If");
        sb.AppendLine("several commits landed between two builds, only the last is recorded, and the");
        sb.AppendLine("others are not visible to you and not visible anywhere in this platform. Do");
        sb.AppendLine("not describe this as a complete list of changes, and do not invent work that");
        sb.AppendLine("is not shown to fill apparent gaps.");
        sb.AppendLine();

        if (BuildsMissingCommitMetadata > 0)
        {
            sb.AppendLine($"{BuildsMissingCommitMetadata} of these builds have NO commit message or author " +
                          "recorded (they predate the platform capturing that). Say nothing about what " +
                          "those builds contained beyond their commit SHA.");
            sb.AppendLine();
        }

        sb.AppendLine("Builds, oldest first:");
        foreach (var c in Commits)
        {
            sb.Append($"- #{c.BuildNumber} [{c.Status}] {c.CommitShort} on {c.Branch}");

            if (!string.IsNullOrWhiteSpace(c.PackageVersion)) sb.Append($" (version {c.PackageVersion})");
            if (c.CommittedAtUtc is { } at) sb.Append($" committed {at:u}");
            sb.AppendLine();

            sb.AppendLine(string.IsNullOrWhiteSpace(c.Subject)
                ? "    (no commit message recorded)"
                : $"    {c.Author ?? "unknown author"}: {c.Subject}");
        }

        sb.AppendLine();
        sb.AppendLine("Note that FAILED and ABORTED builds are included above. A commit that only ever " +
                      "appears on a failed build did not necessarily ship — say so rather than listing " +
                      "it alongside work that succeeded.");

        return sb.ToString();
    }
}
