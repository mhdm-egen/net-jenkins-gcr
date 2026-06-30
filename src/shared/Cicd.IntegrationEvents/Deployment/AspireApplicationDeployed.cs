namespace Cicd.IntegrationEvents.Deployment;

/// <summary>
/// A whole .NET Aspire application was deployed to a Kubernetes namespace (via Aspir8). Emitted by the
/// deployment service on the "deployment.events" channel; downstream services may react.
/// </summary>
public sealed record AspireApplicationDeployed(
    Guid EventId,
    DateTimeOffset OccurredAtUtc,
    Guid RunId,
    Guid ApplicationId,
    string ApplicationName,
    string Namespace) : IIntegrationEvent;
