using System.Text;
using Cicd.Ai;

namespace Cicd.Web.Admin.Services.Deployment;

/// <summary>
/// Grounded, cached explanation of a whole-Aspire-app deploy from its aspirate log.
///
/// Deliberately not failure-only. A run can SUCCEED and still leave the app unreachable — that is
/// exactly what <see cref="DeploymentAdvisories"/> exists to surface — so on a succeeded run this
/// explains the warnings instead. Synthesis tier: the aspirate log is long and noisy, which is the
/// same synthesis problem as pipeline triage.
/// </summary>
public sealed class AspireRunExplainer : IAspireRunExplainer
{
    private static readonly TimeSpan CacheFor = TimeSpan.FromDays(7);

    private const string SystemPrompt =
        "You are a Kubernetes and .NET Aspire engineer helping a developer understand a deployment " +
        "that ran through the aspirate CLI. Ground every statement in the supplied run record and log; " +
        "do NOT invent manifest contents, image digests, kubectl commands, or cluster state that are " +
        "not shown. If the run FAILED, explain what failed and the fix. If the run SUCCEEDED, do not " +
        "manufacture a problem — summarise what was deployed, then explain any warnings and what they " +
        "mean for reaching the app (a succeeded deploy can still be unreachable, e.g. when no ingress " +
        "controller backs the Ingress). Be concise and practical, and quote the relevant log line when " +
        "you can. Close by naming anything the log does not show that would be needed to be certain.";

    private readonly AiExplanationRunner _runner;

    public AspireRunExplainer(AiExplanationRunner runner) => _runner = runner;

    public bool IsConfigured => _runner.IsConfigured;

    public async Task<DeployExplanation> ExplainAsync(
        AspireRunExplainRequest request, CancellationToken ct = default)
    {
        var outcome = await _runner.RunAsync(
            // Status is in the key — an AwaitingPromotion run can later be promoted or rolled back.
            cacheKey: $"aspire-run-explain:v1:{request.RunId}:{request.Status}",
            feature: "explain_aspire_deploy",
            tier: AiModelKind.Synthesis,
            systemPrompt: SystemPrompt,
            groundedPrompt: BuildPrompt(request),
            ttl: CacheFor,
            dimensions: new Dictionary<string, string>
            {
                ["service"] = request.ApplicationName,
                ["environment"] = request.EnvironmentName,
            },
            ct: ct);

        return new DeployExplanation(outcome.Text, outcome.FromCache, outcome.ModelUsed);
    }

    private static string BuildPrompt(AspireRunExplainRequest r)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Aspire application: {r.ApplicationName}");
        sb.AppendLine($"Environment: {r.EnvironmentName}");
        sb.AppendLine($"Cluster / namespace: {r.KubeContext} / {r.Namespace}");
        sb.AppendLine($"Manifest source: {r.ManifestSource}");
        sb.AppendLine($"Version: {(string.IsNullOrWhiteSpace(r.Version) ? "(not recorded)" : r.Version)}");
        sb.AppendLine($"Run status: {r.Status}");
        sb.AppendLine($"Failure reason recorded by the deployment service: " +
                      $"{(string.IsNullOrWhiteSpace(r.FailureReason) ? "(none)" : r.FailureReason)}");
        sb.AppendLine();

        sb.AppendLine("Images the run reported putting on the cluster:");
        if (r.DeployedImages.Count == 0)
            sb.AppendLine("  (none recorded)");
        else
            foreach (var i in r.DeployedImages) sb.AppendLine($"  - {i}");
        sb.AppendLine();

        if (string.IsNullOrWhiteSpace(r.LogTail))
        {
            sb.AppendLine("No deploy log was captured. Base your answer only on the record above.");
        }
        else
        {
            sb.AppendLine(r.IsTruncated
                ? $"Deploy log — the LAST {AspireRunExplainRequest.LogTailChars} characters only; earlier output is not shown:"
                : "Deploy log (complete):");
            sb.AppendLine("```");
            sb.AppendLine(r.LogTail);
            sb.AppendLine("```");
        }

        return sb.ToString();
    }
}
