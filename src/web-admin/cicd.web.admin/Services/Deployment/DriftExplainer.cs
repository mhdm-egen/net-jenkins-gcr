using System.Text;
using Cicd.Ai;
using Deployment.Contracts.AspireApps;

namespace Cicd.Web.Admin.Services.Deployment;

/// <summary>
/// Explains what has drifted on a deployed Aspire app: which workloads are running something other
/// than what the platform last deployed, whether there is an undeployed change waiting, and what that
/// combination implies.
///
/// Interactive tier — the cluster reader has already diffed running images against the last deployed
/// set, so the input is a short structured comparison rather than a log to reason backwards from.
///
/// NOT cached by app id alone: drift is live cluster state that can change between refreshes, so the
/// key includes the observed image set. A stale explanation of a drift that has since been corrected
/// would be actively misleading.
/// </summary>
public sealed class DriftExplainer : IDriftExplainer
{
    /// <summary>Short: this describes live cluster state, which moves.</summary>
    private static readonly TimeSpan CacheFor = TimeSpan.FromHours(6);

    private const string SystemPrompt =
        "You are a Kubernetes engineer explaining configuration drift on a deployed .NET Aspire " +
        "application. Drift means a workload is running an image the platform did not deploy — " +
        "someone or something changed it out of band. Ground every statement in the supplied " +
        "comparison; do NOT invent workload names, image tags, digests, or kubectl output that is not " +
        "given. Distinguish clearly between the two situations, because they need opposite responses: " +
        "an UNDEPLOYED CHANGE means the platform has something newer that has not been rolled out " +
        "yet, while IMAGE DRIFT means the cluster is running something the platform does not know " +
        "about — the second is the one that loses work on the next deploy. Be concise and concrete. " +
        "Structure as: (1) what is actually different, (2) the likely explanation given the evidence, " +
        "(3) what to do, and explicitly whether redeploying would overwrite something. Close with " +
        "anything the data does not show that would settle it.";

    private readonly AiExplanationRunner _runner;

    public DriftExplainer(AiExplanationRunner runner) => _runner = runner;

    public bool IsConfigured => _runner.IsConfigured;

    public async Task<DeployExplanation> ExplainAsync(
        DriftExplainRequest request, CancellationToken ct = default)
    {
        var outcome = await _runner.RunAsync(
            cacheKey: $"drift-explain:v1:{request.ApplicationId}:{request.StateFingerprint()}",
            feature: "explain_drift",
            tier: AiModelKind.Interactive,
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

    private static string BuildPrompt(DriftExplainRequest r)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Application: {r.ApplicationName}");
        sb.AppendLine($"Environment: {r.EnvironmentName}");
        sb.AppendLine($"Cluster / namespace: {r.KubeContext} / {r.Namespace}");
        sb.AppendLine($"Overall health reported by the cluster: {r.OverallHealth}");
        sb.AppendLine();

        sb.AppendLine($"Platform's last deployed version: {r.LastDeployedVersion ?? "(not recorded)"}");
        sb.AppendLine($"Version the platform currently holds: {r.CurrentVersion ?? "(not recorded)"}");
        sb.AppendLine($"Undeployed change waiting: {(r.HasUndeployedChanges ? "YES" : "no")}");
        sb.AppendLine($"Image drift detected: {(r.HasImageDrift ? "YES" : "no")}");
        sb.AppendLine();

        sb.AppendLine("Workloads (running image vs what the platform deployed):");
        if (r.Workloads.Count == 0)
        {
            sb.AppendLine("  (none reported — the namespace may be empty or unreachable)");
        }
        else
        {
            foreach (var w in r.Workloads)
            {
                sb.AppendLine($"  - {w.Name} [{w.Health}] ready {w.ReadyReplicas}/{w.DesiredReplicas}");
                sb.AppendLine($"      running:  {w.Image ?? "(unknown)"}");
                if (w.Drifted)
                    sb.AppendLine($"      DRIFTED — platform expected: {w.ExpectedImage ?? "(not recorded)"}");
            }
        }

        sb.AppendLine();
        sb.AppendLine("This is a comparison of image references only. It does not cover drift in " +
                      "environment variables, config maps, replica counts set outside the platform, " +
                      "or anything applied by another tool — do not claim those are clean.");
        return sb.ToString();
    }
}
