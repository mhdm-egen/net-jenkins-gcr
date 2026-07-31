using Deployment.Contracts.AspireApps;
using Deployment.Contracts.Runs;

namespace Cicd.Web.Admin.Services.Deployment;

/// <summary>The generated explanation plus provenance (cache hit / model used).</summary>
public sealed record DeployExplanation(string Text, bool FromCache, string ModelUsed);

/// <summary>
/// Grounding for a per-service deployment run's failure. Unlike a pipeline run there is no log —
/// only the typed per-step record — so the whole prompt is structured, which is why this runs on the
/// cheaper Interactive tier.
/// </summary>
public sealed record DeployRunExplainRequest(
    Guid RunId,
    string Status,
    string ServiceName,
    string Version,
    string SourceRef,
    string Target,
    string TriggeredBy,
    string? FailureReason,
    string? RemoteImageRef,
    IReadOnlyList<DeployStepFact> Steps)
{
    public static DeployRunExplainRequest FromRun(DeploymentRunDto r) => new(
        RunId: r.Id,
        Status: r.Status.ToString(),
        ServiceName: r.ServiceName,
        Version: r.Version,
        SourceRef: r.SourceRef,
        // Which platform the run targeted changes the remediation entirely, so state it plainly
        // rather than making the model infer it from which fields happen to be populated.
        Target: !string.IsNullOrWhiteSpace(r.CloudRunServiceName)
            ? $"Google Cloud Run service '{r.CloudRunServiceName}' in {r.GcpProject}/{r.Region}"
            : !string.IsNullOrWhiteSpace(r.KubernetesResource)
                ? $"Kubernetes resource '{r.KubernetesResource}'"
                : $"{r.GcpProject}/{r.Region}",
        TriggeredBy: r.TriggeredBy,
        FailureReason: r.FailureReason,
        RemoteImageRef: r.RemoteImageRef,
        Steps: r.Steps.OrderBy(s => s.Order)
            .Select(s => new DeployStepFact(s.Order, s.Kind, s.Status, s.Detail, s.FailureKind))
            .ToList());

    /// <summary>The failure categories present on this run, with what each one means.</summary>
    public IReadOnlyList<string> FailureKindLegend => Steps
        .Select(s => s.FailureKind)
        .Where(k => !string.IsNullOrWhiteSpace(k))
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .Select(k => KindMeanings.TryGetValue(k!, out var meaning) ? $"{k}: {meaning}" : k!)
        .ToList();

    /// <summary>
    /// Mirrors the comments on <c>Deployment.Domain.Runs.StepFailureKind</c>. The deploy pipeline
    /// already classified the failure; handing the model that vocabulary keeps it from re-deriving
    /// the category from the free-text detail.
    /// </summary>
    private static readonly Dictionary<string, string> KindMeanings = new(StringComparer.OrdinalIgnoreCase)
    {
        ["ToolMissing"] = "a required CLI (e.g. crane) is not installed or not on PATH",
        ["RegistryAuth"] = "the registry (Nexus/GAR) rejected the push/pull credentials",
        ["RegistryError"] = "the image copy failed for a non-authentication reason",
        ["CloudRunAuth"] = "the Cloud Run admin API returned Unauthenticated or PermissionDenied",
        ["CloudRunNotFound"] = "the target Cloud Run service does not exist and create is disabled",
        ["Timeout"] = "a readiness poll or deadline was exceeded",
        ["Config"] = "a required input was missing (source ref, GAR repo, project/region, …)",
        ["Unknown"] = "the failure was not classified",
    };
}

/// <summary>One step's outcome as the prompt sees it.</summary>
public sealed record DeployStepFact(int Order, string Kind, string Status, string? Detail, string? FailureKind);

/// <summary>
/// Grounding for a whole-Aspire-app deploy. This one DOES have a log (aspirate CLI output, already
/// tail-trimmed to 16k on the domain side), so it runs on the Synthesis tier like pipeline triage.
/// Applies to succeeded runs too — a run can succeed while emitting warnings that explain why the
/// app is not reachable.
/// </summary>
public sealed record AspireRunExplainRequest(
    Guid RunId,
    string Status,
    string ApplicationName,
    string EnvironmentName,
    string KubeContext,
    string Namespace,
    string ManifestSource,
    string? Version,
    string? FailureReason,
    IReadOnlyList<string> DeployedImages,
    string LogTail)
{
    /// <summary>Defensive: the domain already caps the log at 16k, but don't rely on that here.</summary>
    public const int LogTailChars = 12_000;

    public static AspireRunExplainRequest FromRun(AspireApplicationRunDto r) => new(
        RunId: r.Id,
        Status: r.Status.ToString(),
        ApplicationName: r.ApplicationName,
        EnvironmentName: r.EnvironmentName,
        KubeContext: r.KubeContext,
        Namespace: r.Namespace,
        ManifestSource: r.ManifestSource,
        Version: r.Version,
        FailureReason: r.FailureReason,
        DeployedImages: (r.DeployedImages ?? []).Select(i => $"{i.Workload} → {i.Image}").ToList(),
        LogTail: Tail(r.Log));

    private static string Tail(string? log)
    {
        if (string.IsNullOrWhiteSpace(log)) return string.Empty;
        return log.Length <= LogTailChars ? log : log[^LogTailChars..];
    }

    /// <summary>True when the log was long enough that the prompt only carries its tail.</summary>
    public bool IsTruncated => LogTail.Length >= LogTailChars;
}

/// <summary>
/// Grounding for a drift explanation — the platform's view of what it deployed against what the
/// cluster is actually running.
/// </summary>
public sealed record DriftExplainRequest(
    Guid ApplicationId,
    string ApplicationName,
    string EnvironmentName,
    string KubeContext,
    string Namespace,
    string OverallHealth,
    string? CurrentVersion,
    string? LastDeployedVersion,
    bool HasUndeployedChanges,
    bool HasImageDrift,
    IReadOnlyList<DriftWorkload> Workloads)
{
    /// <summary>
    /// Identifies the observed state, so the cache key changes the moment the cluster does. Drift is
    /// live state — an explanation of a drift that has since been corrected would be worse than none.
    /// </summary>
    public string StateFingerprint()
    {
        var w = string.Join("|", Workloads
            .Select(x => $"{x.Name}={x.Image}/{x.ExpectedImage}/{x.Drifted}")
            .OrderBy(x => x, StringComparer.Ordinal));
        return $"{HasImageDrift}:{HasUndeployedChanges}:{w}".GetHashCode().ToString("x8");
    }

    public static DriftExplainRequest FromStatus(AspireAppStatusDto status) => new(
        ApplicationId: status.ApplicationId,
        ApplicationName: status.ApplicationName,
        EnvironmentName: status.EnvironmentName,
        KubeContext: status.KubeContext ?? "(not recorded)",
        Namespace: status.Namespace ?? "(not recorded)",
        OverallHealth: status.OverallHealth.ToString(),
        CurrentVersion: status.CurrentVersion,
        LastDeployedVersion: status.LastDeployedVersion,
        HasUndeployedChanges: status.HasUndeployedChanges,
        HasImageDrift: status.HasImageDrift,
        Workloads: status.Workloads
            .Select(w => new DriftWorkload(
                w.Name, w.Image, w.ExpectedImage, w.Drifted,
                w.Health.ToString(), w.DesiredReplicas, w.ReadyReplicas))
            .ToList());
}

/// <summary>One workload's running-vs-expected comparison as the prompt sees it.</summary>
public sealed record DriftWorkload(
    string Name,
    string? Image,
    string? ExpectedImage,
    bool Drifted,
    string Health,
    int DesiredReplicas,
    int ReadyReplicas);
