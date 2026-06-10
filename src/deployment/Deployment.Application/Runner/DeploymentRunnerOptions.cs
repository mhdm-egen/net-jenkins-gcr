namespace Deployment.Application.Runner;

/// <summary>
/// Configuration for the deployment runner BackgroundService. Bound from
/// <c>"Deployment:Runner"</c>.
/// </summary>
public sealed class DeploymentRunnerOptions
{
    public const string SectionName = "Deployment:Runner";

    /// <summary>
    /// When true, the runner is registered as a HostedService and polls for
    /// queued deployments. Disable when running the runner out-of-process in
    /// a separate worker host (the API stays a read+write surface only).
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Seconds between polls when the queue is empty. The runner is
    /// edge-triggered for a single batch — once it picks up a row it works
    /// through whatever's queued before sleeping again.
    /// </summary>
    public int PollIntervalSeconds { get; set; } = 5;

    /// <summary>
    /// Hard timeout the runner gives the adapter to finish. Adapters that
    /// need longer (canary observation) should be moved off the synchronous
    /// pipeline — until then this caps the worst case.
    /// </summary>
    public int AdapterTimeoutSeconds { get; set; } = 300;
}
