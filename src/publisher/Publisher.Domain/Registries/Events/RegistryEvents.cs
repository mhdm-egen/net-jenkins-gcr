using Publisher.Domain.Common;

namespace Publisher.Domain.Registries.Events;

public sealed record RemoteRegistryRegistered(
    Guid RegistryId,
    string Name,
    RegistryProvider Provider,
    string RegistryHost,
    string RepositoryPath,
    DateTimeOffset OccurredAtUtc) : IDomainEvent;

public sealed record RemoteRegistryUpdated(
    Guid RegistryId,
    string Name,
    RegistryProvider Provider,
    string RegistryHost,
    string RepositoryPath,
    DateTimeOffset OccurredAtUtc) : IDomainEvent;

public sealed record RemoteRegistryActivationChanged(
    Guid RegistryId,
    bool Enabled,
    DateTimeOffset OccurredAtUtc) : IDomainEvent;

public sealed record RemoteRegistryDefaultChanged(
    Guid RegistryId,
    bool IsDefault,
    DateTimeOffset OccurredAtUtc) : IDomainEvent;
