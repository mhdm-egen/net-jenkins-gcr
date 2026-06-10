using Jenkins.Domain.Common;

namespace Jenkins.Domain.Builds;

/// <summary>
/// A thing a build produced — a NuGet package or a container image
/// (<see cref="ArtifactKind"/>). Owns the registry <see cref="ArtifactPublication"/>
/// rows that record where it was pushed. <see cref="Digest"/> is the integrity
/// hash (<c>sha256:…</c> for an image; the <c>.nupkg</c> hash for a package) that
/// becomes the Release provenance ArtifactSha256 on handoff.
///
/// Child entity of <see cref="Build"/>; created and mutated only via the root.
/// </summary>
public sealed class BuildArtifact : Entity<Guid>
{
    public Guid BuildId { get; private set; }
    public ArtifactKind Kind { get; private set; }

    /// <summary>Package id (<c>Egen.Foo</c>) or image repo (<c>egen/web-apphost</c>).</summary>
    public string Name { get; private set; }

    /// <summary>PackageVersion for NuGet; primary semantic tag for an image.</summary>
    public string Version { get; private set; }

    /// <summary><c>sha256:…</c> for an image; <c>.nupkg</c> hash for a package.</summary>
    public string Digest { get; private set; }

    public long? SizeBytes { get; private set; }
    public DateTimeOffset ProducedAtUtc { get; private set; }

    private readonly List<ArtifactPublication> _publications = new();
    public IReadOnlyCollection<ArtifactPublication> Publications => _publications.AsReadOnly();

    public bool IsContainerImage => Kind == ArtifactKind.ContainerImage;

    private BuildArtifact()
    {
        Name = string.Empty;
        Version = string.Empty;
        Digest = string.Empty;
    }

    internal BuildArtifact(
        Guid id,
        Guid buildId,
        ArtifactKind kind,
        string name,
        string version,
        string digest,
        long? sizeBytes,
        DateTimeOffset producedAtUtc)
    {
        if (id == Guid.Empty) throw new ArgumentException("Id cannot be empty.", nameof(id));
        if (buildId == Guid.Empty)
            throw new ArgumentException("BuildId cannot be empty.", nameof(buildId));
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Name cannot be empty.", nameof(name));
        if (string.IsNullOrWhiteSpace(version))
            throw new ArgumentException("Version cannot be empty.", nameof(version));
        if (string.IsNullOrWhiteSpace(digest))
            throw new ArgumentException("Digest cannot be empty.", nameof(digest));

        Id = id;
        BuildId = buildId;
        Kind = kind;
        Name = name.Trim();
        Version = version.Trim();
        Digest = digest.Trim();
        SizeBytes = sizeBytes;
        ProducedAtUtc = producedAtUtc;
    }

    /// <summary>
    /// Record where this artifact was pushed. Idempotent on (registry, reference):
    /// re-recording the same push returns the existing row rather than duplicating.
    /// </summary>
    internal ArtifactPublication AddPublication(
        Guid publicationId,
        PublicationRegistry registry,
        string reference,
        IEnumerable<string>? tags,
        PublicationStatus status,
        DateTimeOffset publishedAtUtc)
    {
        var existing = _publications.FirstOrDefault(p =>
            p.Registry == registry &&
            string.Equals(p.Reference, reference?.Trim(), StringComparison.Ordinal));
        if (existing is not null) return existing;

        var publication = new ArtifactPublication(
            publicationId, Id, registry, reference!, tags, status, publishedAtUtc);
        _publications.Add(publication);
        return publication;
    }

    /// <summary>The successful Nexus push for this artifact, if any (the handoff source).</summary>
    public ArtifactPublication? NexusPublication() =>
        _publications.FirstOrDefault(p =>
            p.Status == PublicationStatus.Pushed &&
            p.Registry is PublicationRegistry.NexusDocker or PublicationRegistry.NexusNuGet);
}
