using Microsoft.AspNetCore.SignalR.Client;
using Cicd.Web.Admin.Services.Ci;
using Cicd.Web.Admin.Services.Deployment;

namespace Cicd.Web.Admin.Services;

/// <summary>Server-broadcast run-completion messages (camelCase JSON; SignalR binds case-insensitively).</summary>
public sealed record PipelineRunCompletedMsg(Guid RunId, string PipelineName, string Status, string? FailureReason);
public sealed record DeploymentRunCompletedMsg(Guid RunId, string Status, string Title, string? Detail);

/// <summary>
/// The single per-circuit owner of the two SignalR hubs (<c>hubs/pipeline-runs</c> RunCompleted /
/// <c>hubs/deployment-runs</c> DeploymentCompleted). Anything in the app subscribes to its events instead of
/// opening its own connection: <see cref="CompletionToasts"/> for toasts, pages for live table refresh, the
/// AppBar badge for pending counts. Best-effort — a hub that's down just means no live updates this circuit.
/// </summary>
public sealed class RunEventsService : IAsyncDisposable
{
    private readonly JenkinsApiClient _jenkins;
    private readonly DeploymentApiClient _deployment;
    private readonly ILogger<RunEventsService> _logger;
    private HubConnection? _pipelineHub;
    private HubConnection? _deployHub;
    private bool _started;

    public RunEventsService(JenkinsApiClient jenkins, DeploymentApiClient deployment, ILogger<RunEventsService> logger)
    {
        _jenkins = jenkins;
        _deployment = deployment;
        _logger = logger;
    }

    /// <summary>Fired with the payload when a pipeline run completes (for the toast text).</summary>
    public event Action<PipelineRunCompletedMsg>? PipelineRunCompleted;
    /// <summary>Fired with the payload when a deployment run completes.</summary>
    public event Action<DeploymentRunCompletedMsg>? DeploymentRunCompleted;
    /// <summary>Convenience: fired whenever any run completes — subscribers that just want to reload.</summary>
    public event Action? Changed;

    /// <summary>Idempotently start the hub connections. Call once from an app-wide component's first render (JS/
    /// interactive circuit is available by then).</summary>
    public async Task EnsureStartedAsync()
    {
        if (_started) return;
        _started = true;

        try
        {
            _pipelineHub = new HubConnectionBuilder()
                .WithUrl(new Uri(_jenkins.BaseAddress, "hubs/pipeline-runs"))
                .WithAutomaticReconnect()
                .Build();
            _pipelineHub.On<PipelineRunCompletedMsg>("RunCompleted", m => { PipelineRunCompleted?.Invoke(m); Changed?.Invoke(); });
            await _pipelineHub.StartAsync();
        }
        catch (Exception ex) { _logger.LogDebug(ex, "pipeline-runs hub unavailable; live updates disabled for this circuit."); }

        try
        {
            _deployHub = new HubConnectionBuilder()
                .WithUrl(new Uri(_deployment.BaseAddress, "hubs/deployment-runs"))
                .WithAutomaticReconnect()
                .Build();
            _deployHub.On<DeploymentRunCompletedMsg>("DeploymentCompleted", m => { DeploymentRunCompleted?.Invoke(m); Changed?.Invoke(); });
            await _deployHub.StartAsync();
        }
        catch (Exception ex) { _logger.LogDebug(ex, "deployment-runs hub unavailable; live updates disabled for this circuit."); }
    }

    public async ValueTask DisposeAsync()
    {
        if (_pipelineHub is not null) await _pipelineHub.DisposeAsync();
        if (_deployHub is not null) await _deployHub.DisposeAsync();
    }
}
