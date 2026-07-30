using System.Security.Cryptography;
using System.Text;
using Cicd.Ai;

namespace Cicd.Web.Admin.Services.Sca;

/// <summary>
/// Explains a project's license posture: what the analyzer flagged, which findings actually matter for
/// shipping, and in what order to deal with them.
///
/// Interactive tier — <see cref="LicenseAnalyzer"/> has already categorised every component and
/// written a reason per conflict, so this is a rollup over structured findings rather than reasoning
/// out of a noisy log.
///
/// Cached on a FINGERPRINT of the analysis, not the build number: rebuilds of an unchanged dependency
/// set have identical license posture and should share one answer.
/// </summary>
public sealed class LicenseExplainer : ILicenseExplainer
{
    private static readonly TimeSpan CacheFor = TimeSpan.FromDays(30);

    private const string SystemPrompt =
        "You are advising a .NET engineering team on the license posture of their dependency graph. " +
        "A static analyzer has already categorised every component and written a reason for each " +
        "flagged finding — trust that work and do not re-derive it. Your job is the shipping " +
        "decision: which findings actually block or constrain distribution, which are routine, and " +
        "what order to address them in. Ground every statement in the supplied findings; do NOT " +
        "invent package names, license texts, version numbers, or obligations that are not given. " +
        "You are not a lawyer and must not imply otherwise: say plainly that anything consequential " +
        "needs review by someone qualified to make license calls. Note that undeclared licences are " +
        "an absence of information, not evidence of a permissive one. Be concise. Structure as: " +
        "(1) the headline — is there anything here that stops us shipping, (2) the findings that " +
        "matter and why, in priority order, (3) what to do next.";

    private readonly AiExplanationRunner _runner;

    public LicenseExplainer(AiExplanationRunner runner) => _runner = runner;

    public bool IsConfigured => _runner.IsConfigured;

    public async Task<LicenseExplanation> ExplainAsync(
        LicenseExplainRequest request, CancellationToken ct = default)
    {
        var outcome = await _runner.RunAsync(
            cacheKey: $"license-explain:v1:{ShortHash(request.Fingerprint())}",
            feature: "explain_licenses",
            tier: AiModelKind.Interactive,
            systemPrompt: SystemPrompt,
            groundedPrompt: BuildPrompt(request),
            ttl: CacheFor,
            ct: ct);

        return new LicenseExplanation(outcome.Text, outcome.FromCache, outcome.ModelUsed);
    }

    /// <summary>The fingerprint can be long; hash it so the cache key stays bounded.</summary>
    private static string ShortHash(string value)
        => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)))[..16];

    private static string BuildPrompt(LicenseExplainRequest r)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Project / image: {r.Subject}");
        sb.AppendLine($"Root project's own license category: {r.RootCategory}");
        sb.AppendLine($"Components in the dependency graph: {r.TotalComponents}");
        sb.AppendLine();

        sb.AppendLine("Component counts by license category:");
        sb.AppendLine($"  permissive: {r.Counts.Permissive}");
        sb.AppendLine($"  weak copyleft (LGPL/MPL/EPL): {r.Counts.WeakCopyleft}");
        sb.AppendLine($"  strong copyleft (GPL): {r.Counts.StrongCopyleft}");
        sb.AppendLine($"  network copyleft (AGPL): {r.Counts.NetworkCopyleft}");
        sb.AppendLine($"  public domain: {r.Counts.PublicDomain}");
        sb.AppendLine($"  proprietary / commercial: {r.Counts.Proprietary}");
        sb.AppendLine($"  undeclared (no license metadata in the SBOM): {r.Counts.Unknown}");
        sb.AppendLine();

        if (r.Conflicts.Count == 0)
        {
            sb.AppendLine("The analyzer flagged NO conflicts. Say so plainly and briefly — do not " +
                          "manufacture concerns to fill the space. You may note the undeclared count " +
                          "if it is non-zero, since that is missing information rather than a clean bill.");
        }
        else
        {
            sb.AppendLine($"Flagged findings ({r.Conflicts.Count}), each with the analyzer's own reason:");
            foreach (var c in r.Conflicts.OrderByDescending(x => x.Severity))
            {
                sb.AppendLine($"  - [{c.Severity}] {c.Component} — {c.Category}");
                sb.AppendLine($"      {c.Reason}");
            }
        }

        sb.AppendLine();
        sb.AppendLine("The analyzer deliberately does not model classpath exceptions, LGPL static " +
                      "vs dynamic linking, or commercial dual-licensing. Do not assume it accounted " +
                      "for those, and say so if one of them would change a finding.");
        return sb.ToString();
    }
}
