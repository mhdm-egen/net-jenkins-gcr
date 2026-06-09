namespace Deployment.Infrastructure.Runner;

/// <summary>
/// Options for <see cref="GoogleCloudRunDeploymentAdapter"/>. Bound from
/// configuration section <c>"Deployment:GoogleCloudRun"</c>.
///
/// Authentication uses Application Default Credentials (ADC) — set
/// <c>GOOGLE_APPLICATION_CREDENTIALS</c> to a service-account key file, or run
/// under Workload Identity / a GCE/GKE metadata server. The bound service
/// account needs <c>roles/run.developer</c> on the target service and
/// <c>roles/iam.serviceAccountUser</c> on the runtime service account. Credentials
/// are intentionally ambient (ADC) — never configured here — per the project's
/// secret-handling rules.
/// </summary>
public sealed class GoogleCloudRunOptions
{
    public const string SectionName = "Deployment:GoogleCloudRun";

    /// <summary>
    /// How long to poll the Cloud Run service for the new revision to become
    /// Ready before giving up. The runner imposes its own outer ceiling
    /// (<c>Deployment:Runner:AdapterTimeoutSeconds</c>); keep this at or below it.
    /// </summary>
    public int ReadinessTimeoutSeconds { get; set; } = 300;

    /// <summary>Delay between readiness polls.</summary>
    public int ReadinessPollSeconds { get; set; } = 5;
}
