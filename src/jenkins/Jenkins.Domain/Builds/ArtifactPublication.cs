using Jenkins.Domain.Common;

namespace Jenkins.Domain.Builds;

/// <summary>
/// A push of a <see cref="BuildArtifact"/> to a registry. A container image lands
/// in Nexus Docker; a package lands in Nexus NuGet. <see cref="Reference"/> is the
/// immutable coordinate — for images, the digest ref
/// (<c>host/path@sha256:…</c>) that the deployment handoff carries as the Release
/// ArtifactUri.
///
/// Child entity of <see cref="BuildArtifact"/>; created only via the aggregate root.
/// </summary>
public sealed class ArtifactPublication : Entity<Guid>
{
    public Guid BuildArtifactId { get; private set; }
    public PublicationRegistry Registry { get; private set; }

    /// <summary>Immutable coordinate: <c>host/path@sha256:…</c> for images, feed URL for NuGet.</summary>
    public string Reference { get; private set; }

    public PublicationStatus Status { get; private set; }
    public DateTimeOffset PublishedAtUtc { get; private set; }

    // Not readonly: EF assigns this field on materialization via the tags
    // value-converter (a JSON column). Written only by the ctor and EF.
    private List<string> _tags = new();

    /// <summary>For images, the tri-tag set (<c>latest</c>, <c>ci-42</c>, <c>7a4b9c1</c>).</summary>
    public IReadOnlyCollection<string> Tags => _tags.AsReadOnly();

    private ArtifactPublication()
    {
        Reference = string.Empty;
    }

    internal ArtifactPublication(
        Guid id,
        Guid buildArtifactId,
        PublicationRegistry registry,
        string reference,
        IEnumerable<string>? tags,
        PublicationStatus status,
        DateTimeOffset publishedAtUtc)
    {
        if (id == Guid.Empty) throw new ArgumentException("Id cannot be empty.", nameof(id));
        if (buildArtifactId == Guid.Empty)
            throw new ArgumentException("BuildArtifactId cannot be empty.", nameof(buildArtifactId));
        if (string.IsNullOrWhiteSpace(reference))
            throw new ArgumentException("Reference cannot be empty.", nameof(reference));

        Id = id;
        BuildArtifactId = buildArtifactId;
        Registry = registry;
        Reference = reference.Trim();
        Status = status;
        PublishedAtUtc = publishedAtUtc;

        if (tags is not null)
        {
            foreach (var tag in tags.Where(t => !string.IsNullOrWhiteSpace(t)))
                _tags.Add(tag.Trim());
        }
    }
}
