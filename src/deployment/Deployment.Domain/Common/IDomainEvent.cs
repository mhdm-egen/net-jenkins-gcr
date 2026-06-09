namespace Deployment.Domain.Common;

/// <summary>
/// Marker for things-that-happened in the domain. Concrete events are records
/// declared in the relevant aggregate's folder (e.g. <c>DeploymentSucceeded</c>
/// in <c>Deployments/</c>). Dispatched by the Infrastructure's UnitOfWork after
/// SaveChangesAsync via Wolverine's in-process bus.
/// </summary>
public interface IDomainEvent
{
    DateTimeOffset OccurredAtUtc { get; }
}
