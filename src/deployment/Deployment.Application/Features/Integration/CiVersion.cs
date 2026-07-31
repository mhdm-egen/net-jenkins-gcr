using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;

namespace Deployment.Application.Features.Integration;

/// <summary>
/// Orders CI package versions of the form <c>{base}-ci.{buildNumber}.g{sha}</c>
/// (e.g. <c>1.0.0-ci.29.ge0a1643</c>), stamped by <c>jenkins/publish/aspire/Jenkinsfile</c>.
///
/// This exists because nothing else in the publish path is orderable: <c>AspireAppPublished</c> carries a
/// Guid build id and an <i>ingestion</i> timestamp, so a build backfill stamps every event with
/// effectively the same instant. The build number inside the version is the only ordering signal.
/// </summary>
internal static class CiVersion
{
    private static readonly Regex Rx = new(
        @"^(?<base>.+)-ci\.(?<n>\d+)\.g[0-9a-fA-F]+$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public static bool TryParse(string? version, [NotNullWhen(true)] out string? baseVersion, out int buildNumber)
    {
        baseVersion = null;
        buildNumber = 0;
        if (string.IsNullOrWhiteSpace(version)) return false;

        var m = Rx.Match(version.Trim());
        if (!m.Success) return false;
        if (!int.TryParse(m.Groups["n"].Value, out buildNumber)) return false;

        baseVersion = m.Groups["base"].Value;
        return true;
    }

    /// <summary>
    /// True only when <paramref name="incoming"/> is provably an EARLIER build of the same base version
    /// than <paramref name="current"/>.
    ///
    /// Fails open — returns false, letting the publish through — when either side is missing or doesn't
    /// parse, and when the base versions differ. That second rule is deliberate: a recreated Jenkins job
    /// restarts BUILD_NUMBER at 1, and a strict global comparison would then wedge the app permanently
    /// with no route back except a manual deploy. Refusing to compare across bases bounds the damage to
    /// "we might redeploy" rather than "we can never deploy again".
    ///
    /// Equality is not treated as older: an identical republish is already stopped by
    /// <c>AspireApplication.ApplyPublishedManifest</c>, so the two checks stay disjoint.
    /// </summary>
    public static bool IsOlderThan(string? incoming, string? current)
    {
        if (!TryParse(incoming, out var incomingBase, out var incomingNumber)) return false;
        if (!TryParse(current, out var currentBase, out var currentNumber)) return false;
        if (!string.Equals(incomingBase, currentBase, StringComparison.Ordinal)) return false;

        return incomingNumber < currentNumber;
    }
}
