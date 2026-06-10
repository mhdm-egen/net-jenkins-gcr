using Deployment.Domain.Common;

namespace Deployment.Domain.Deployments;

/// <summary>
/// Append-only audit row for a <see cref="Deployment"/>. Captures state
/// transitions, smoke-test outcomes, current-pin fallbacks, and any other
/// operationally-interesting moment in the deployment's lifetime.
///
/// <see cref="Detail"/> is free-form (JSON string by convention) so the model
/// doesn't need to grow with every new event subtype.
/// </summary>
public sealed class DeploymentEvent : Entity<Guid>
{
    public Guid DeploymentId { get; private set; }
    public DateTimeOffset Timestamp { get; private set; }
    public string EventType { get; private set; }
    public string? Detail { get; private set; }

    private DeploymentEvent()
    {
        EventType = string.Empty;
    }

    internal DeploymentEvent(Guid id, Guid deploymentId, DateTimeOffset timestamp, string eventType, string? detail)
    {
        if (id == Guid.Empty) throw new ArgumentException("Id cannot be empty.", nameof(id));
        if (deploymentId == Guid.Empty)
            throw new ArgumentException("DeploymentId cannot be empty.", nameof(deploymentId));
        if (string.IsNullOrWhiteSpace(eventType))
            throw new ArgumentException("EventType cannot be empty.", nameof(eventType));

        Id = id;
        DeploymentId = deploymentId;
        Timestamp = timestamp;
        EventType = eventType.Trim();
        Detail = string.IsNullOrWhiteSpace(detail) ? null : detail;
    }
}
