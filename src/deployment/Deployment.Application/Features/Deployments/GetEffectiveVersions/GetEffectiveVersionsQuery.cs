namespace Deployment.Application.Features.Deployments.GetEffectiveVersions;

/// <summary>
/// Q1′ (decisions §10.4): for every service of <paramref name="ApplicationId"/>,
/// the running version per <c>DeploymentTarget</c> in
/// <paramref name="EnvironmentId"/>. The env-level Q1 is a derived view over
/// this — collapse per-service if all targets agree, else show "Mixed".
/// Pure query.
/// </summary>
public sealed record GetEffectiveVersionsQuery(
    Guid ApplicationId,
    Guid EnvironmentId);

public sealed record EffectiveVersions(
    IReadOnlyList<EffectiveVersionEntry> Entries);

public sealed record EffectiveVersionEntry(
    Guid ServiceId,
    string ServiceName,
    Guid TargetId,
    string TargetResourceId,
    string Region,
    Guid? RunningReleaseId,
    string? SemanticVersion,
    DateTimeOffset? CompletedAtUtc);
