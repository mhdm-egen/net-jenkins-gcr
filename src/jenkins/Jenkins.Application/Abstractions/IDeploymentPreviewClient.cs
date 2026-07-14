namespace Jenkins.Application.Abstractions;

/// <summary>
/// Port to the deployment service's preview-teardown webhook. On PR close/merge the git-webhook
/// handler calls this to tear down the matching preview environment. Implemented in Infrastructure
/// as a typed <c>HttpClient</c> over <c>POST /api/deployment/previews/webhook</c> (base URL
/// <c>Deployment:ApiBaseUrl</c>). Best-effort: a transport failure is logged, not fatal to the ack.
/// </summary>
public interface IDeploymentPreviewClient
{
    /// <summary>Ask the deployment service to tear down the preview matching (<paramref name="appName"/>,
    /// <paramref name="key"/>) for the given git <paramref name="action"/> (closed/merged/deleted).</summary>
    Task TeardownPreviewAsync(string appName, string key, string action, CancellationToken ct = default);
}
