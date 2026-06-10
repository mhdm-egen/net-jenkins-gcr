using Jenkins.Domain.Common;

namespace Jenkins.Domain.Builds;

/// <summary>
/// The commit a build was produced from (CI decision #5: embedded value object,
/// not a separate aggregate). Maps one-to-one onto the git block of
/// <c>build-info.json</c>. SHA + branch are required; author/message/timestamp are
/// best-effort and may be absent depending on the checkout.
/// </summary>
public sealed class SourceRevision : ValueObject
{
    /// <summary>Full commit SHA (<c>build-info.json</c> GitCommitHash).</summary>
    public string CommitSha { get; }

    /// <summary>Short commit SHA (GitCommitShort) — the <c>g7a4b9c1</c> in a package version.</summary>
    public string CommitShort { get; }

    public string Branch { get; }
    public string? Author { get; }
    public string? Message { get; }
    public DateTimeOffset? CommittedAtUtc { get; }

    public SourceRevision(
        string commitSha,
        string commitShort,
        string branch,
        string? author = null,
        string? message = null,
        DateTimeOffset? committedAtUtc = null)
    {
        if (string.IsNullOrWhiteSpace(commitSha))
            throw new ArgumentException("CommitSha cannot be empty.", nameof(commitSha));
        if (string.IsNullOrWhiteSpace(commitShort))
            throw new ArgumentException("CommitShort cannot be empty.", nameof(commitShort));
        if (string.IsNullOrWhiteSpace(branch))
            throw new ArgumentException("Branch cannot be empty.", nameof(branch));

        CommitSha = commitSha.Trim();
        CommitShort = commitShort.Trim();
        Branch = branch.Trim();
        Author = string.IsNullOrWhiteSpace(author) ? null : author.Trim();
        Message = string.IsNullOrWhiteSpace(message) ? null : message.Trim();
        CommittedAtUtc = committedAtUtc;
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return CommitSha;
        yield return CommitShort;
        yield return Branch;
        yield return Author;
        yield return Message;
        yield return CommittedAtUtc;
    }
}
