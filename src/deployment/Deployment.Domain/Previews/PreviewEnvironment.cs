using Deployment.Domain.Common;
using Deployment.Domain.Previews.Events;

namespace Deployment.Domain.Previews;

/// <summary>
/// An ephemeral deploy of an <see cref="AspireApps.AspireApplication"/>'s manifest into its own namespace
/// (e.g. per pull request). Created <see cref="PreviewStatus.Creating"/> — raising
/// <see cref="PreviewEnvironmentRequested"/> to drive the executor — then Active/Failed, and finally
/// <see cref="PreviewStatus.TornDown"/> when the namespace is deleted (manual teardown or the TTL sweeper).
/// Snapshots the target coordinates so the executor needs no catalog re-read.
/// </summary>
public sealed class PreviewEnvironment : AggregateRoot<Guid>
{
    public Guid ApplicationId { get; private set; }
    public string ApplicationName { get; private set; }

    /// <summary>The PR number / branch label this preview tracks (slugified). Unique per app while live.</summary>
    public string Key { get; private set; }

    public string KubeContext { get; private set; }
    public string Namespace { get; private set; }
    public string ManifestSource { get; private set; }
    public string? Version { get; private set; }

    public PreviewStatus Status { get; private set; }
    public string TriggeredBy { get; private set; }
    public string? Log { get; private set; }
    public string? FailureReason { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }
    /// <summary>When the TTL sweeper should tear this down. Null = no expiry.</summary>
    public DateTimeOffset? ExpiresAtUtc { get; private set; }
    public DateTimeOffset? ActivatedAtUtc { get; private set; }
    public DateTimeOffset? TornDownAtUtc { get; private set; }

    private PreviewEnvironment()
    {
        ApplicationName = string.Empty;
        Key = string.Empty;
        KubeContext = string.Empty;
        Namespace = string.Empty;
        ManifestSource = string.Empty;
        TriggeredBy = string.Empty;
    }

    public PreviewEnvironment(
        Guid id, Guid applicationId, string applicationName, string key,
        string kubeContext, string @namespace, string manifestSource, string? version,
        string triggeredBy, DateTimeOffset createdAtUtc, DateTimeOffset? expiresAtUtc)
    {
        if (id == Guid.Empty) throw new ArgumentException("Id cannot be empty.", nameof(id));
        if (applicationId == Guid.Empty) throw new ArgumentException("ApplicationId cannot be empty.", nameof(applicationId));
        Id = id;
        ApplicationId = applicationId;
        ApplicationName = applicationName?.Trim() ?? string.Empty;
        Key = Require(key, nameof(key));
        KubeContext = kubeContext?.Trim() ?? string.Empty;
        Namespace = Require(@namespace, nameof(@namespace));
        ManifestSource = Require(manifestSource, nameof(manifestSource));
        Version = Clean(version);
        TriggeredBy = string.IsNullOrWhiteSpace(triggeredBy) ? "manual" : triggeredBy.Trim();
        Status = PreviewStatus.Creating;
        CreatedAtUtc = createdAtUtc;
        ExpiresAtUtc = expiresAtUtc;
        RaiseEvent(new PreviewEnvironmentRequested(Id, createdAtUtc));
    }

    public void MarkActive(string? log, DateTimeOffset occurredAtUtc)
    {
        if (Status == PreviewStatus.TornDown) return;
        Status = PreviewStatus.Active;
        Log = Trim(log);
        FailureReason = null;
        ActivatedAtUtc = occurredAtUtc;
    }

    public void MarkFailed(string reason, string? log, DateTimeOffset occurredAtUtc)
    {
        if (Status == PreviewStatus.TornDown) return;
        Status = PreviewStatus.Failed;
        FailureReason = string.IsNullOrWhiteSpace(reason) ? "Unknown error." : reason.Trim();
        Log = Trim(log);
    }

    public void MarkTornDown(DateTimeOffset occurredAtUtc)
    {
        if (Status == PreviewStatus.TornDown) return;
        Status = PreviewStatus.TornDown;
        TornDownAtUtc = occurredAtUtc;
    }

    private static string Require(string value, string name)
        => string.IsNullOrWhiteSpace(value) ? throw new ArgumentException($"{name} cannot be empty.", name) : value.Trim();
    private static string? Clean(string? v) => string.IsNullOrWhiteSpace(v) ? null : v.Trim();
    private static string? Trim(string? log) => string.IsNullOrEmpty(log) ? log : (log.Length > 16000 ? log[^16000..] : log);
}
