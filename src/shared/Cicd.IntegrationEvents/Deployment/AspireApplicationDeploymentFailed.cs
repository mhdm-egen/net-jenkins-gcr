namespace Cicd.IntegrationEvents.Deployment;

/// <summary>
/// A whole .NET Aspire application deployment (via Aspir8) failed. Emitted by the deployment service
/// on the "deployment.events" channel; downstream services may react.
/// </summary>
public sealed record AspireApplicationDeploymentFailed(
    Guid EventId,
    DateTimeOffset OccurredAtUtc,
    Guid RunId,
    Guid ApplicationId,
    string ApplicationName,
    string Reason) : IIntegrationEvent;
