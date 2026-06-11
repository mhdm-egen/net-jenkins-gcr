using Deployment.Domain.Common;
using Deployment.Domain.ContainerImages.Events;

namespace Deployment.Domain.ContainerImages;

/// <summary>
/// A reusable container-image <em>coordinate</em> — the registry address
/// (<see cref="Registry"/> + <see cref="Repository"/> + <see cref="Name"/>) that a
/// deployment Service is backed by, decoupling the service from a hard-coded artifact
/// URI. A release/deploy selects a coordinate + a tag (defaulting to
/// <see cref="DefaultTag"/>), which is resolved to an immutable digest at authoring
/// time and pinned onto the Release.
///
/// Because the resolved digest lives on the (immutable) Release, the coordinate is only
/// needed at authoring time — deactivating one hides it from pickers without affecting
/// existing releases. See <c>docs/deployment/container-image-source.md</c>.
/// </summary>
public sealed class ContainerImage : AggregateRoot<Guid>
{
    /// <summary>Registry host, e.g. <c>nexus:8082</c>.</summary>
    public string Registry { get; private set; }

    /// <summary>Repository/path within the registry, e.g. <c>docker-private</c>.</summary>
    public string Repository { get; private set; }

    /// <summary>Logical image name, e.g. <c>orders-api</c>.</summary>
    public string Name { get; private set; }

    /// <summary>Default tag selector used when none is chosen explicitly (e.g. <c>latest</c>).</summary>
    public string DefaultTag { get; private set; }

    public bool IsActive { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }

    /// <summary>Pull base reference without a tag/digest: <c>{Registry}/{Repository}/{Name}</c>.</summary>
    public string BaseRef => $"{Registry}/{Repository}/{Name}";

    private ContainerImage()
    {
        Registry = string.Empty;
        Repository = string.Empty;
        Name = string.Empty;
        DefaultTag = string.Empty;
    }

    public ContainerImage(
        Guid id,
        string registry,
        string repository,
        string name,
        string? defaultTag,
        DateTimeOffset createdAtUtc)
    {
        if (id == Guid.Empty) throw new ArgumentException("Id cannot be empty.", nameof(id));

        Id = id;
        Registry = Require(registry, nameof(registry));
        Repository = Require(repository, nameof(repository));
        Name = Require(name, nameof(name));
        DefaultTag = NormalizeTag(defaultTag);
        IsActive = true;
        CreatedAtUtc = createdAtUtc;

        RaiseEvent(new ContainerImageRegistered(
            Id, Registry, Repository, Name, DefaultTag, createdAtUtc));
    }

    public void ChangeDefaultTag(string? defaultTag, DateTimeOffset occurredAtUtc)
    {
        var tag = NormalizeTag(defaultTag);
        if (string.Equals(DefaultTag, tag, StringComparison.Ordinal)) return;
        DefaultTag = tag;
        RaiseEvent(new ContainerImageDefaultTagChanged(Id, DefaultTag, occurredAtUtc));
    }

    public void Deactivate(DateTimeOffset occurredAtUtc)
    {
        if (!IsActive) return;
        IsActive = false;
        RaiseEvent(new ContainerImageDeactivated(Id, occurredAtUtc));
    }

    public void Reactivate(DateTimeOffset occurredAtUtc)
    {
        if (IsActive) return;
        IsActive = true;
        RaiseEvent(new ContainerImageReactivated(Id, occurredAtUtc));
    }

    private static string NormalizeTag(string? tag) =>
        string.IsNullOrWhiteSpace(tag) ? "latest" : tag.Trim();

    private static string Require(string value, string field) =>
        string.IsNullOrWhiteSpace(value)
            ? throw new ArgumentException($"{field} cannot be empty.", field)
            : value.Trim();
}
