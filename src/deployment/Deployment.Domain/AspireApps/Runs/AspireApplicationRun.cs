using Deployment.Domain.Common;
using Deployment.Domain.Runs;
using Deployment.Domain.AspireApps.Runs.Events;

namespace Deployment.Domain.AspireApps.Runs;

/// <summary>
/// One deployment of an <see cref="AspireApplication"/> via Aspir8. Created Pending (raises
/// <see cref="AspireApplicationRunRequested"/> to drive the executor), then Running →
/// Succeeded/Failed. Snapshots the target coordinates so the executor needs no catalog re-read, and
/// captures the aspirate CLI output as <see cref="Log"/>. Reuses <see cref="DeploymentRunStatus"/>.
/// </summary>
public sealed class AspireApplicationRun : AggregateRoot<Guid>
{
    public Guid ApplicationId { get; private set; }
    public string ApplicationName { get; private set; }

    // Target snapshot.
    public Guid EnvironmentId { get; private set; }
    public string EnvironmentName { get; private set; }
    public string KubeContext { get; private set; }
    public string Namespace { get; private set; }
    public string ManifestSource { get; private set; }
    public string? Version { get; private set; }

    public DeploymentRunStatus Status { get; private set; }
    public string TriggeredBy { get; private set; }
    public string? Log { get; private set; }
    public string? FailureReason { get; private set; }
    public DateTimeOffset RequestedAtUtc { get; private set; }
    public DateTimeOffset? CompletedAtUtc { get; private set; }

    private AspireApplicationRun()
    {
        ApplicationName = string.Empty;
        EnvironmentName = string.Empty;
        KubeContext = string.Empty;
        Namespace = string.Empty;
        ManifestSource = string.Empty;
        TriggeredBy = string.Empty;
    }

    public AspireApplicationRun(
        Guid id, Guid applicationId, string applicationName,
        Guid environmentId, string environmentName, string kubeContext, string @namespace,
        string manifestSource, string? version, string triggeredBy, DateTimeOffset requestedAtUtc)
    {
        if (id == Guid.Empty) throw new ArgumentException("Id cannot be empty.", nameof(id));
        Id = id;
        ApplicationId = applicationId;
        ApplicationName = applicationName?.Trim() ?? string.Empty;
        EnvironmentId = environmentId;
        EnvironmentName = environmentName?.Trim() ?? string.Empty;
        KubeContext = kubeContext?.Trim() ?? string.Empty;
        Namespace = @namespace?.Trim() ?? string.Empty;
        ManifestSource = manifestSource?.Trim() ?? string.Empty;
        Version = string.IsNullOrWhiteSpace(version) ? null : version.Trim();
        TriggeredBy = string.IsNullOrWhiteSpace(triggeredBy) ? "manual" : triggeredBy.Trim();
        Status = DeploymentRunStatus.Pending;
        RequestedAtUtc = requestedAtUtc;
        RaiseEvent(new AspireApplicationRunRequested(Id, ApplicationId, requestedAtUtc));
    }

    public void Start() { if (Status == DeploymentRunStatus.Pending) Status = DeploymentRunStatus.Running; }

    public void Succeed(string? log, DateTimeOffset completedAtUtc)
    {
        if (Status is DeploymentRunStatus.Succeeded or DeploymentRunStatus.Failed) return;
        Status = DeploymentRunStatus.Succeeded;
        Log = Trim(log);
        CompletedAtUtc = completedAtUtc;
        RaiseEvent(new AspireApplicationRunSucceeded(Id, ApplicationId, ApplicationName, Namespace, completedAtUtc));
    }

    public void Fail(string reason, string? log, DateTimeOffset completedAtUtc)
    {
        if (Status is DeploymentRunStatus.Succeeded or DeploymentRunStatus.Failed) return;
        Status = DeploymentRunStatus.Failed;
        FailureReason = string.IsNullOrWhiteSpace(reason) ? "Unknown error." : reason.Trim();
        Log = Trim(log);
        CompletedAtUtc = completedAtUtc;
        RaiseEvent(new AspireApplicationRunFailed(Id, ApplicationId, ApplicationName, FailureReason, completedAtUtc));
    }

    private static string? Trim(string? log)
        => string.IsNullOrEmpty(log) ? log : (log.Length > 16000 ? log[^16000..] : log);
}
