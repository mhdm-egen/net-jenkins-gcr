using Deployment.Domain.Runs.Events;

namespace Deployment.Application.Features.Integration;

/// <summary>
/// Producer edge (deployment → bus): translates the internal <see cref="DeploymentRunSucceeded"/>
/// domain event into the cross-service <see cref="Cicd.IntegrationEvents.Deployment.ServiceDeployed"/>
/// integration event. Discovered by Wolverine; the cascaded return is published through the SQL
/// outbox onto the "deployment.events" channel.
/// </summary>
public sealed class DeploymentRunSucceededTranslator
{
    public Cicd.IntegrationEvents.Deployment.ServiceDeployed Handle(DeploymentRunSucceeded evt)
        => new(
            EventId: Guid.NewGuid(),
            OccurredAtUtc: evt.OccurredAtUtc,
            RunId: evt.RunId,
            ServiceId: evt.ServiceId,
            ServiceName: evt.ServiceName,
            EnvironmentId: evt.EnvironmentId,
            ContainerName: evt.ContainerName,
            Version: evt.Version,
            GcpProject: evt.GcpProject,
            Region: evt.Region,
            CloudRunServiceName: evt.CloudRunServiceName,
            RemoteImageRef: evt.RemoteImageRef,
            CloudRunRevision: evt.CloudRunRevision);
}
