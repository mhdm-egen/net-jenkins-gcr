namespace Cicd.IntegrationEvents.Ci;

/// <summary>
/// A container image produced by a CI build was published to the registry. Emitted by the
/// Jenkins CI service; downstream services (deployment, publishing, notifications) may react.
/// </summary>
public sealed record ContainerPublished(
    Guid EventId,
    DateTimeOffset OccurredAtUtc,
    Guid BuildId,
    Guid RepositoryId,
    string ContainerName,
    string ArtifactUri,
    string Version,
    string CommitSha,
    /// <summary>
    /// When the commit behind this image was authored — the start of commit→production lead time.
    /// Nullable on purpose, twice over: the CI side's <c>SourceRevision.CommittedAtUtc</c> is
    /// best-effort, and an optional trailing member keeps messages already sitting in the outbox
    /// (published before this field existed) deserializable.
    /// </summary>
    DateTimeOffset? CommittedAtUtc = null) : IIntegrationEvent;
