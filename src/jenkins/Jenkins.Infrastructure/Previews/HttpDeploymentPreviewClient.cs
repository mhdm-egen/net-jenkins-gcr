using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using Jenkins.Application.Abstractions;

namespace Jenkins.Infrastructure.Previews;

/// <summary>
/// <see cref="IDeploymentPreviewClient"/> as a typed <c>HttpClient</c> over the deployment service's
/// normalized preview webhook (<c>POST /api/deployment/previews/webhook</c>). The base address is set
/// at registration from <c>Deployment:ApiBaseUrl</c>. Best-effort — the deployment webhook always
/// returns 200 and the TTL sweeper is the fallback, so a transport failure is logged, not thrown.
/// </summary>
internal sealed class HttpDeploymentPreviewClient : IDeploymentPreviewClient
{
    private readonly HttpClient _http;
    private readonly ILogger<HttpDeploymentPreviewClient> _logger;

    public HttpDeploymentPreviewClient(HttpClient http, ILogger<HttpDeploymentPreviewClient> logger)
    {
        _http = http;
        _logger = logger;
    }

    public async Task TeardownPreviewAsync(string appName, string key, string action, CancellationToken ct = default)
    {
        try
        {
            using var resp = await _http.PostAsJsonAsync(
                "api/deployment/previews/webhook",
                new { appName, key, action }, ct).ConfigureAwait(false);
            _logger.LogInformation("[webhook] preview teardown app={App} key={Key} action={Action} -> {Status}.",
                appName, key, action, (int)resp.StatusCode);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[webhook] preview teardown call failed for app={App} key={Key} (TTL sweeper will reap it).", appName, key);
        }
    }
}
