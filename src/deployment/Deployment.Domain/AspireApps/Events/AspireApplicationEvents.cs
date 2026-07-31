using Deployment.Domain.Common;

namespace Deployment.Domain.AspireApps.Events;

public sealed record AspireApplicationRegistered(Guid ApplicationId, string Name, DateTimeOffset OccurredAtUtc) : IDomainEvent;
public sealed record AspireApplicationUpdated(Guid ApplicationId, string Name, DateTimeOffset OccurredAtUtc) : IDomainEvent;
public sealed record AspireApplicationAutoDeployChanged(Guid ApplicationId, bool AutoDeploy, DateTimeOffset OccurredAtUtc) : IDomainEvent;

/// <summary>A CI <c>AspireAppPublished</c> refreshed this app's manifest source/version (name-matched).</summary>
public sealed record AspireApplicationManifestPublished(Guid ApplicationId, string Name, string ManifestSource, string? Version, DateTimeOffset OccurredAtUtc) : IDomainEvent;

/// <summary>The app's workloads were removed from the cluster; the registration survives so it can be
/// redeployed. <paramref name="Namespaces"/> is what was actually deleted (one for Direct, up to two
/// for blue-green when a green slot was parked awaiting promotion).</summary>
public sealed record AspireApplicationUninstalled(Guid ApplicationId, string Name, IReadOnlyList<string> Namespaces, DateTimeOffset OccurredAtUtc) : IDomainEvent;
