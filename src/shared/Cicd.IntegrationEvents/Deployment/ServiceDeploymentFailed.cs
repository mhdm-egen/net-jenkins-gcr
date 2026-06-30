namespace Cicd.IntegrationEvents.Deployment;

/// <summary>
/// A service's deployment to a Cloud Run environment failed. Emitted by the deployment service on
/// the "deployment.events" channel; downstream services (CI feedback, notifications) may react.
/// <see cref="FailedStep"/> is the step that failed (e.g. "GarPush") and <see cref="Category"/> the
/// failure kind (e.g. "RegistryAuth"); both may be null when the cause isn't a categorized step.
/// </summary>
public sealed record ServiceDeploymentFailed(
    Guid EventId,
    DateTimeOffset OccurredAtUtc,
    Guid RunId,
    Guid ServiceId,
    Guid EnvironmentId,
    string Reason,
    string? FailedStep,
    string? Category) : IIntegrationEvent;
