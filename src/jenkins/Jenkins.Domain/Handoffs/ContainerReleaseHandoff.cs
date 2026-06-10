using Jenkins.Domain.Common;
using Jenkins.Domain.Handoffs.Events;

namespace Jenkins.Domain.Handoffs;

/// <summary>
/// The integration record (handoff §3) — durable proof that a container build of
/// a tracked repo became a <c>Release</c> in the deployment microservice. Holds
/// <see cref="DeploymentReleaseId"/>, the only foreign handle across the service
/// boundary (a value reference, not an FK).
///
/// Created <see cref="HandoffStatus.Pending"/> when a build is promoted (manually
/// or by the auto-publish handler). The application layer calls the deployment
/// Releases API, then settles this row via <see cref="MarkPublished"/> /
/// <see cref="MarkFailed"/>. On a 409 (duplicate version) the handler treats it as
/// already-published and calls <see cref="MarkPublished"/> with the existing id —
/// the natural idempotency from sending PackageVersion (CI decision #4).
/// </summary>
public sealed class ContainerReleaseHandoff : AggregateRoot<Guid>
{
    public Guid BuildId { get; private set; }
    public Guid BuildArtifactId { get; private set; }
    public Guid DeployableComponentId { get; private set; }
    public Guid RepositoryId { get; private set; }
    public Guid DeployableUnitId { get; private set; }

    /// <summary>The Release.Id returned by the deployment service; null until published.</summary>
    public Guid? DeploymentReleaseId { get; private set; }

    public string SemanticVersion { get; private set; }

    /// <summary>The Nexus digest ref sent as the Release ArtifactUri (CI decision #6).</summary>
    public string ArtifactUri { get; private set; }

    public HandoffStatus Status { get; private set; }
    public string RequestedByPrincipal { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset? SettledAtUtc { get; private set; }
    public string? FailureReason { get; private set; }

    private ContainerReleaseHandoff()
    {
        SemanticVersion = string.Empty;
        ArtifactUri = string.Empty;
        RequestedByPrincipal = string.Empty;
    }

    public ContainerReleaseHandoff(
        Guid id,
        Guid buildId,
        Guid buildArtifactId,
        Guid deployableComponentId,
        Guid repositoryId,
        Guid deployableUnitId,
        string semanticVersion,
        string artifactUri,
        string requestedByPrincipal,
        DateTimeOffset createdAtUtc)
    {
        if (id == Guid.Empty) throw new ArgumentException("Id cannot be empty.", nameof(id));
        if (buildId == Guid.Empty)
            throw new ArgumentException("BuildId cannot be empty.", nameof(buildId));
        if (buildArtifactId == Guid.Empty)
            throw new ArgumentException("BuildArtifactId cannot be empty.", nameof(buildArtifactId));
        if (deployableComponentId == Guid.Empty)
            throw new ArgumentException("DeployableComponentId cannot be empty.", nameof(deployableComponentId));
        if (repositoryId == Guid.Empty)
            throw new ArgumentException("RepositoryId cannot be empty.", nameof(repositoryId));
        if (deployableUnitId == Guid.Empty)
            throw new ArgumentException("DeployableUnitId cannot be empty.", nameof(deployableUnitId));
        if (string.IsNullOrWhiteSpace(semanticVersion))
            throw new ArgumentException("SemanticVersion cannot be empty.", nameof(semanticVersion));
        if (string.IsNullOrWhiteSpace(artifactUri))
            throw new ArgumentException("ArtifactUri cannot be empty.", nameof(artifactUri));
        if (string.IsNullOrWhiteSpace(requestedByPrincipal))
            throw new ArgumentException("RequestedByPrincipal cannot be empty.", nameof(requestedByPrincipal));

        Id = id;
        BuildId = buildId;
        BuildArtifactId = buildArtifactId;
        DeployableComponentId = deployableComponentId;
        RepositoryId = repositoryId;
        DeployableUnitId = deployableUnitId;
        SemanticVersion = semanticVersion.Trim();
        ArtifactUri = artifactUri.Trim();
        RequestedByPrincipal = requestedByPrincipal.Trim();
        Status = HandoffStatus.Pending;
        CreatedAtUtc = createdAtUtc;

        RaiseEvent(new HandoffRequested(
            Id, BuildId, BuildArtifactId, DeployableComponentId, DeployableUnitId,
            SemanticVersion, ArtifactUri, RequestedByPrincipal, createdAtUtc));
    }

    public void MarkPublished(Guid deploymentReleaseId, DateTimeOffset occurredAtUtc)
    {
        if (Status != HandoffStatus.Pending)
            throw new InvalidOperationException(
                $"Cannot publish handoff {Id}: status is {Status}, expected Pending.");
        if (deploymentReleaseId == Guid.Empty)
            throw new ArgumentException("DeploymentReleaseId cannot be empty.", nameof(deploymentReleaseId));

        Status = HandoffStatus.Published;
        DeploymentReleaseId = deploymentReleaseId;
        SettledAtUtc = occurredAtUtc;
        RaiseEvent(new HandoffPublished(Id, deploymentReleaseId, occurredAtUtc));
    }

    public void MarkFailed(string failureReason, DateTimeOffset occurredAtUtc)
    {
        if (Status != HandoffStatus.Pending)
            throw new InvalidOperationException(
                $"Cannot fail handoff {Id}: status is {Status}, expected Pending.");
        if (string.IsNullOrWhiteSpace(failureReason))
            throw new ArgumentException("FailureReason cannot be empty.", nameof(failureReason));

        Status = HandoffStatus.Failed;
        FailureReason = failureReason.Trim();
        SettledAtUtc = occurredAtUtc;
        RaiseEvent(new HandoffFailed(Id, FailureReason, occurredAtUtc));
    }

    /// <summary>Operator declined to promote this build (manual gate, CI decision #3).</summary>
    public void MarkSkipped(string reason, DateTimeOffset occurredAtUtc)
    {
        if (Status != HandoffStatus.Pending)
            throw new InvalidOperationException(
                $"Cannot skip handoff {Id}: status is {Status}, expected Pending.");
        if (string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException("Reason cannot be empty.", nameof(reason));

        Status = HandoffStatus.Skipped;
        FailureReason = reason.Trim();
        SettledAtUtc = occurredAtUtc;
        RaiseEvent(new HandoffSkipped(Id, FailureReason, occurredAtUtc));
    }
}
