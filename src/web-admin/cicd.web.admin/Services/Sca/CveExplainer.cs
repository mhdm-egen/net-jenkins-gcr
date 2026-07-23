using System.Text;
using Cicd.Web.Admin.Services.Ai;
using Microsoft.Extensions.Caching.Distributed;

namespace Cicd.Web.Admin.Services.Sca;

/// <summary>
/// Grounded, cached CVE explanations. Builds a cited prompt from SBOM data, runs it on the
/// interactive model tier via <see cref="IAiInsightService"/> (usage flows to the metering
/// ledger automatically), and caches the answer in Redis keyed by (cve, affected components)
/// — stable inputs, so repeat views cost nothing.
/// </summary>
public sealed class CveExplainer : ICveExplainer
{
    private static readonly DistributedCacheEntryOptions CacheOptions = new()
    {
        AbsoluteExpirationRelativeToNow = TimeSpan.FromDays(7),
    };

    private const string SystemPrompt =
        "You are a security analyst helping a .NET/DevOps engineer triage a dependency vulnerability. " +
        "Explain the CVE in the context of the affected package(s) provided. Ground every statement in the " +
        "supplied data — do NOT invent CVSS scores, attack vectors, or fixed version numbers that are not " +
        "given; if the data is limited, say so plainly. Be concise and practical. This is advisory only, not " +
        "a security guarantee. Structure the answer as: (1) what the vulnerability is, (2) whether and how it " +
        "affects the listed package, (3) severity in plain terms, (4) recommended direction — usually upgrading " +
        "the affected package, noting the exact fixed version must be confirmed against the advisory.";

    private readonly IAiInsightService _ai;
    private readonly IDistributedCache _cache;

    public CveExplainer(IAiInsightService ai, IDistributedCache cache)
    {
        _ai = ai;
        _cache = cache;
    }

    public bool IsConfigured => _ai.IsConfigured;

    public async Task<CveExplanation> ExplainAsync(CveExplainRequest request, CancellationToken ct = default)
    {
        var cacheKey = BuildCacheKey(request);

        var cached = await _cache.GetStringAsync(cacheKey, ct);
        if (cached is { Length: > 0 })
            return new CveExplanation(cached, FromCache: true, ModelUsed: "cache");

        var insight = await _ai.GetInsightAsync(new AiInsightRequest(
            Feature: "explain_cve",
            SystemPrompt: SystemPrompt,
            GroundedPrompt: BuildPrompt(request),
            Model: AiModelKind.Interactive), ct);

        if (!string.IsNullOrWhiteSpace(insight.Text))
            await _cache.SetStringAsync(cacheKey, insight.Text, CacheOptions, ct);

        return new CveExplanation(insight.Text, FromCache: false, ModelUsed: insight.ModelUsed);
    }

    private static string BuildCacheKey(CveExplainRequest r)
    {
        var affected = string.Join(",", r.AffectedComponents.OrderBy(x => x, StringComparer.Ordinal));
        return $"cve-explain:v1:{r.CveId}:{affected}";
    }

    private static string BuildPrompt(CveExplainRequest r)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"CVE / advisory id: {r.CveId}");
        sb.AppendLine($"Severity (as reported by the scanner): {(string.IsNullOrEmpty(r.Severity) ? "unrated" : r.Severity)}");
        if (!string.IsNullOrEmpty(r.Source)) sb.AppendLine($"Advisory source: {r.Source}");
        sb.AppendLine();

        sb.AppendLine("Affected package(s) in this project's dependency graph:");
        if (r.AffectedComponents.Count == 0)
            sb.AppendLine("  (not resolved)");
        else
            foreach (var c in r.AffectedComponents) sb.AppendLine($"  - {c}");
        sb.AppendLine();

        sb.AppendLine("Advisory description (verbatim from the SBOM):");
        sb.AppendLine(string.IsNullOrWhiteSpace(r.Description) ? "  (none provided)" : r.Description);

        if (r.ReferenceUrls.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("Reference links:");
            foreach (var u in r.ReferenceUrls) sb.AppendLine($"  - {u}");
        }

        sb.AppendLine();
        sb.AppendLine("Note: CVSS score, vector, and fixed-version ranges are NOT included in this SBOM data. " +
                      "Do not fabricate them; base your answer only on the facts above.");
        return sb.ToString();
    }
}
