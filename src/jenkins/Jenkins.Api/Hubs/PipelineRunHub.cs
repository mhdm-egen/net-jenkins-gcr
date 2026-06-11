using Jenkins.Application.Abstractions;
using Jenkins.Application.Features.PipelineRuns;
using Microsoft.AspNetCore.SignalR;

namespace Jenkins.Api.Hubs;

/// <summary>
/// Live pipeline-run stream. A client calls <see cref="Subscribe"/> with a run id to join the
/// run's group; the executor pushes <c>StepChanged</c>, <c>ConsoleAppended</c>, and
/// <c>RunSettled</c> messages to it via <see cref="PipelineRunNotifier"/>. On subscribe the
/// caller is replayed the current run snapshot + buffered console so late joiners catch up.
/// </summary>
public sealed class PipelineRunHub : Hub
{
    private readonly GetPipelineRunByIdHandler _getRun;
    private readonly IPipelineRunConsoleBuffer _console;

    public PipelineRunHub(GetPipelineRunByIdHandler getRun, IPipelineRunConsoleBuffer console)
    {
        _getRun = getRun;
        _console = console;
    }

    public async Task Subscribe(Guid runId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, GroupName(runId)).ConfigureAwait(false);

        var run = await _getRun.HandleAsync(new GetPipelineRunByIdQuery(runId)).ConfigureAwait(false);
        if (run is not null)
            await Clients.Caller.SendAsync("RunSnapshot", run).ConfigureAwait(false);

        foreach (var segment in _console.Snapshot(runId))
            await Clients.Caller.SendAsync("ConsoleAppended",
                new { runId, jobName = segment.JobName, buildNumber = 0, text = segment.Text }).ConfigureAwait(false);
    }

    public Task Unsubscribe(Guid runId)
        => Groups.RemoveFromGroupAsync(Context.ConnectionId, GroupName(runId));

    internal static string GroupName(Guid runId) => $"run:{runId}";
}
