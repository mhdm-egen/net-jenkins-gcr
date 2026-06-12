using Publisher.Domain.Common;
using Publisher.Domain.Promotions.Events;

namespace Publisher.Domain.Promotions;

/// <summary>
/// A single attempt to push one inventory container to one remote registry — the execution +
/// audit record. Created <see cref="PromotionStatus.Pending"/> (which raises
/// <see cref="PromotionRequested"/> to drive the executor), then settled
/// <see cref="PromotionStatus.Succeeded"/> / <see cref="PromotionStatus.Failed"/>. The source and
/// remote refs are computed up front so the executor only needs the credential at run time.
///
/// Container identity is snapshotted onto the promotion so the success integration event can be
/// emitted without re-loading the container aggregate.
/// </summary>
public sealed class Promotion : AggregateRoot<Guid>
{
    public Guid ContainerId { get; private set; }
    public Guid RegistryId { get; private set; }

    /// <summary>The rule that triggered this push, or null for a manual promotion.</summary>
    public Guid? RuleId { get; private set; }

    /// <summary>Free-text origin, e.g. <c>rule:push-to-gar</c> or <c>manual:mike</c>.</summary>
    public string TriggeredBy { get; private set; }

    public string SourceRef { get; private set; }
    public string RemoteRef { get; private set; }

    // Snapshot of the container for the success event.
    public Guid RepositoryId { get; private set; }
    public string ContainerName { get; private set; }
    public string Version { get; private set; }
    public string RegistryName { get; private set; }

    public PromotionStatus Status { get; private set; }
    public string? FailureReason { get; private set; }
    public DateTimeOffset RequestedAtUtc { get; private set; }
    public DateTimeOffset? CompletedAtUtc { get; private set; }

    private Promotion()
    {
        TriggeredBy = string.Empty;
        SourceRef = string.Empty;
        RemoteRef = string.Empty;
        ContainerName = string.Empty;
        Version = string.Empty;
        RegistryName = string.Empty;
    }

    public Promotion(
        Guid id,
        Guid containerId,
        Guid registryId,
        string registryName,
        Guid? ruleId,
        string triggeredBy,
        string sourceRef,
        string remoteRef,
        Guid repositoryId,
        string containerName,
        string version,
        DateTimeOffset requestedAtUtc)
    {
        if (id == Guid.Empty) throw new ArgumentException("Id cannot be empty.", nameof(id));
        if (containerId == Guid.Empty) throw new ArgumentException("ContainerId cannot be empty.", nameof(containerId));
        if (registryId == Guid.Empty) throw new ArgumentException("RegistryId cannot be empty.", nameof(registryId));
        if (string.IsNullOrWhiteSpace(sourceRef)) throw new ArgumentException("SourceRef cannot be empty.", nameof(sourceRef));
        if (string.IsNullOrWhiteSpace(remoteRef)) throw new ArgumentException("RemoteRef cannot be empty.", nameof(remoteRef));

        Id = id;
        ContainerId = containerId;
        RegistryId = registryId;
        RegistryName = registryName?.Trim() ?? string.Empty;
        RuleId = ruleId;
        TriggeredBy = string.IsNullOrWhiteSpace(triggeredBy) ? "system" : triggeredBy.Trim();
        SourceRef = sourceRef.Trim();
        RemoteRef = remoteRef.Trim();
        RepositoryId = repositoryId;
        ContainerName = containerName?.Trim() ?? string.Empty;
        Version = version?.Trim() ?? string.Empty;
        Status = PromotionStatus.Pending;
        RequestedAtUtc = requestedAtUtc;

        RaiseEvent(new PromotionRequested(Id, ContainerId, RegistryId, SourceRef, RemoteRef, requestedAtUtc));
    }

    public void Succeed(DateTimeOffset completedAtUtc)
    {
        if (Status != PromotionStatus.Pending) return;
        Status = PromotionStatus.Succeeded;
        CompletedAtUtc = completedAtUtc;
        RaiseEvent(new PromotionSucceeded(
            Id, ContainerId, RegistryId, RegistryName, RepositoryId, ContainerName, Version,
            SourceRef, RemoteRef, completedAtUtc));
    }

    public void Fail(string reason, DateTimeOffset completedAtUtc)
    {
        if (Status != PromotionStatus.Pending) return;
        Status = PromotionStatus.Failed;
        FailureReason = string.IsNullOrWhiteSpace(reason) ? "Unknown error." : reason.Trim();
        CompletedAtUtc = completedAtUtc;
        RaiseEvent(new PromotionFailed(Id, ContainerId, RegistryId, FailureReason, completedAtUtc));
    }
}
