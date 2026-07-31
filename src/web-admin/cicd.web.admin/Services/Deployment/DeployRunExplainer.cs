using System.Text;
using Cicd.Ai;

namespace Cicd.Web.Admin.Services.Deployment;

/// <summary>
/// Grounded, cached triage of a failed per-service deployment run.
///
/// Runs on the INTERACTIVE tier, unlike pipeline triage. The deploy pipeline already classified the
/// failure into a typed <c>StepFailureKind</c>, so the whole prompt is a short structured step
/// record — the model is explaining and recommending, not inferring a cause out of a noisy log.
/// Paying Opus rates for that would be waste.
/// </summary>
public sealed class DeployRunExplainer : IDeployRunExplainer
{
    private static readonly TimeSpan CacheFor = TimeSpan.FromDays(7);

    private const string SystemPrompt =
        "You are a DevOps engineer helping a .NET developer fix a failed deployment. The deploy " +
        "pipeline has already classified the failure into a category — trust that classification and " +
        "explain what it means for this specific run. Ground every statement in the supplied step " +
        "record; do NOT invent commands, IAM roles, registry paths, or config keys that are not given. " +
        "Be concise and concrete, and lead with the fix rather than a restatement of the error. " +
        "Structure the answer as: (1) which step failed and what the category means here, " +
        "(2) the most likely cause given the target platform, (3) the specific fix to try first, " +
        "(4) how to confirm it worked. If the record is too thin to be confident, say so.";

    private readonly AiExplanationRunner _runner;

    public DeployRunExplainer(AiExplanationRunner runner) => _runner = runner;

    public bool IsConfigured => _runner.IsConfigured;

    public async Task<DeployExplanation> ExplainAsync(
        DeployRunExplainRequest request, CancellationToken ct = default)
    {
        var outcome = await _runner.RunAsync(
            // Status is in the key: a run parked in AwaitingPromotion can later be promoted or
            // rolled back, and the old explanation would no longer describe it.
            cacheKey: $"deploy-run-explain:v1:{request.RunId}:{request.Status}",
            feature: "explain_deploy_failure",
            tier: AiModelKind.Interactive,
            systemPrompt: SystemPrompt,
            groundedPrompt: BuildPrompt(request),
            ttl: CacheFor,
            dimensions: new Dictionary<string, string> { ["service"] = request.ServiceName },
            ct: ct);

        return new DeployExplanation(outcome.Text, outcome.FromCache, outcome.ModelUsed);
    }

    private static string BuildPrompt(DeployRunExplainRequest r)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Service: {r.ServiceName}");
        sb.AppendLine($"Version: {r.Version}");
        sb.AppendLine($"Source ref: {r.SourceRef}");
        sb.AppendLine($"Deploy target: {r.Target}");
        sb.AppendLine($"Run status: {r.Status}");
        sb.AppendLine($"Triggered by: {r.TriggeredBy}");
        if (!string.IsNullOrWhiteSpace(r.RemoteImageRef))
            sb.AppendLine($"Image reference: {r.RemoteImageRef}");
        sb.AppendLine($"Failure reason recorded by the deployment service: " +
                      $"{(string.IsNullOrWhiteSpace(r.FailureReason) ? "(none)" : r.FailureReason)}");
        sb.AppendLine();

        sb.AppendLine("Steps, in order:");
        if (r.Steps.Count == 0)
        {
            sb.AppendLine("  (no steps recorded — the run failed before the first step ran)");
        }
        else
        {
            foreach (var s in r.Steps)
            {
                sb.Append($"  {s.Order}. {s.Kind} — {s.Status}");
                if (!string.IsNullOrWhiteSpace(s.FailureKind)) sb.Append($" [category: {s.FailureKind}]");
                sb.AppendLine();
                if (!string.IsNullOrWhiteSpace(s.Detail)) sb.AppendLine($"       detail: {s.Detail}");
            }
        }

        if (r.FailureKindLegend.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("What the failure categories on this run mean:");
            foreach (var line in r.FailureKindLegend) sb.AppendLine($"  - {line}");
        }

        sb.AppendLine();
        sb.AppendLine("This run has no console log — the step record above is the complete evidence. " +
                      "Do not speculate about output you cannot see.");
        return sb.ToString();
    }
}
