using Publisher.Domain.Common;
using Publisher.Domain.Containers.Events;

namespace Publisher.Domain.Containers;

/// <summary>
/// Inventory record of a single container image observed in the local Nexus docker
/// registry — materialized from the CI <c>ContainerPublished</c> integration event.
/// One row per distinct (RepositoryId, ContainerName, Version); the upstream event is
/// poll-driven and can repeat, so re-observations update the existing record rather
/// than minting duplicates.
///
/// This is the source pool the publisher promotes <i>from</i>. Being recorded here does
/// NOT make a container "publishable" — that is expressed by a <see cref="Channels.PublishChannel"/>
/// pointing at it.
/// </summary>
public sealed class PublishableContainer : AggregateRoot<Guid>
{
    public Guid RepositoryId { get; private set; }

    /// <summary>The CI build aggregate id (a Guid; not the numeric Jenkins build number).</summary>
    public Guid BuildId { get; private set; }

    /// <summary>Image name, e.g. <c>egen/web-apphost</c>.</summary>
    public string ContainerName { get; private set; }

    /// <summary>CI package/semantic version, e.g. <c>1.0.0-ci.42.g7a4b9c1</c>. May be empty if unrecorded upstream.</summary>
    public string Version { get; private set; }

    /// <summary>Git commit the build was produced from.</summary>
    public string CommitSha { get; private set; }

    /// <summary>Nexus pull reference for the image (host/path:tag or @sha256:… as recorded upstream).</summary>
    public string ArtifactUri { get; private set; }

    /// <summary>The sha256 image digest, parsed from <see cref="ArtifactUri"/> when it carries one; otherwise null.</summary>
    public string? ImageDigest { get; private set; }

    public DateTimeOffset FirstSeenAtUtc { get; private set; }
    public DateTimeOffset LastSeenAtUtc { get; private set; }

    /// <summary>Whether this container participates in promotion (rules + manual). New records are active.</summary>
    public bool IsActive { get; private set; }

    /// <summary>How the record entered inventory: the CI bus event, or a manual add from the UI.</summary>
    public ContainerSource Source { get; private set; }

    private PublishableContainer()
    {
        ContainerName = string.Empty;
        Version = string.Empty;
        CommitSha = string.Empty;
        ArtifactUri = string.Empty;
    }

    public PublishableContainer(
        Guid id,
        Guid repositoryId,
        Guid buildId,
        string containerName,
        string version,
        string commitSha,
        string artifactUri,
        DateTimeOffset observedAtUtc,
        ContainerSource source = ContainerSource.Bus)
    {
        if (id == Guid.Empty) throw new ArgumentException("Id cannot be empty.", nameof(id));
        if (string.IsNullOrWhiteSpace(containerName))
            throw new ArgumentException("ContainerName cannot be empty.", nameof(containerName));
        if (string.IsNullOrWhiteSpace(artifactUri))
            throw new ArgumentException("ArtifactUri cannot be empty.", nameof(artifactUri));

        Id = id;
        RepositoryId = repositoryId;
        BuildId = buildId;
        ContainerName = containerName.Trim();
        Version = version?.Trim() ?? string.Empty;
        CommitSha = commitSha?.Trim() ?? string.Empty;
        ArtifactUri = artifactUri.Trim();
        ImageDigest = ParseDigest(ArtifactUri);
        FirstSeenAtUtc = observedAtUtc;
        LastSeenAtUtc = observedAtUtc;
        IsActive = true;
        Source = source;

        RaiseEvent(new ContainerRecorded(Id, RepositoryId, BuildId, ContainerName, Version, ArtifactUri, ImageDigest, observedAtUtc));
    }

    public void Deactivate(DateTimeOffset occurredAtUtc)
    {
        if (!IsActive) return;
        IsActive = false;
        LastSeenAtUtc = occurredAtUtc;
        RaiseEvent(new ContainerActivationChanged(Id, IsActive, occurredAtUtc));
    }

    public void Reactivate(DateTimeOffset occurredAtUtc)
    {
        if (IsActive) return;
        IsActive = true;
        LastSeenAtUtc = occurredAtUtc;
        RaiseEvent(new ContainerActivationChanged(Id, IsActive, occurredAtUtc));
    }

    /// <summary>
    /// Re-observation of the same container (the upstream poll fired again). Refreshes the
    /// reference/digest if they changed and bumps <see cref="LastSeenAtUtc"/>. Idempotent —
    /// raises no event when nothing changed.
    /// </summary>
    public void Reobserve(Guid buildId, string commitSha, string artifactUri, DateTimeOffset observedAtUtc)
    {
        LastSeenAtUtc = observedAtUtc;

        var changed = false;
        if (!string.IsNullOrWhiteSpace(artifactUri) && !string.Equals(ArtifactUri, artifactUri.Trim(), StringComparison.Ordinal))
        {
            ArtifactUri = artifactUri.Trim();
            ImageDigest = ParseDigest(ArtifactUri);
            changed = true;
        }
        if (buildId != Guid.Empty && buildId != BuildId) { BuildId = buildId; changed = true; }
        if (!string.IsNullOrWhiteSpace(commitSha) && !string.Equals(CommitSha, commitSha.Trim(), StringComparison.Ordinal))
        {
            CommitSha = commitSha.Trim();
            changed = true;
        }

        if (changed)
            RaiseEvent(new ContainerReferenceUpdated(Id, ArtifactUri, ImageDigest, observedAtUtc));
    }

    /// <summary>Extracts a <c>sha256:…</c> digest from a pull reference of the form <c>repo@sha256:…</c>.</summary>
    private static string? ParseDigest(string artifactUri)
    {
        var at = artifactUri.IndexOf("@sha256:", StringComparison.OrdinalIgnoreCase);
        return at >= 0 ? artifactUri[(at + 1)..] : null;
    }
}
