namespace Deployment.Application.Abstractions;

/// <summary>
/// Pushes a terminal deployment-run notification to all connected clients (SignalR), so the UI can
/// raise an app-wide completion toast. Implemented in the API host where the hub lives; the handler
/// depends only on this port.
/// </summary>
public interface IDeploymentRunNotifier
{
    /// <param name="kind">Run type discriminator ("deployment" | "aspire") so the UI can deep-link the toast to the
    /// correct run-detail page — both run types broadcast over this one channel.</param>
    Task RunCompletedAsync(Guid runId, string status, string title, string? detail, string kind, CancellationToken cancellationToken = default);
}
