using Jenkins.Application.Abstractions;
using Jenkins.Client;
using Jenkins.Domain.Abstractions;
using Jenkins.Domain.Pipelines;
using Jenkins.Domain.PipelineRuns;
using Jenkins.Domain.SourceRepositories;
using Jenkins.Orchestrator;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Jenkins.Infrastructure.PipelineRuns;

/// <summary>
/// Background engine that executes queued pipeline runs server-side. Reuses
/// <see cref="IPipelineOrchestrator"/> to trigger the Jenkins jobs, streams live step/console
/// updates via <see cref="IPipelineRunNotifier"/> + the console buffer, then records the step
/// results on the <see cref="PipelineRun"/> aggregate and settles it — whose domain events
/// translate to integration events on the bus.
/// </summary>
public sealed class PipelineRunExecutorService : BackgroundService
{
    private readonly IPipelineRunQueue _queue;
    private readonly IServiceScopeFactory _scopes;
    private readonly IPipelineRunNotifier _notifier;
    private readonly IPipelineRunConsoleBuffer _console;
    private readonly IPipelineRunCancellation _cancellation;
    private readonly TimeProvider _clock;
    private readonly ILogger<PipelineRunExecutorService> _logger;

    public PipelineRunExecutorService(
        IPipelineRunQueue queue,
        IServiceScopeFactory scopes,
        IPipelineRunNotifier notifier,
        IPipelineRunConsoleBuffer console,
        IPipelineRunCancellation cancellation,
        TimeProvider clock,
        ILogger<PipelineRunExecutorService> logger)
    {
        _queue = queue;
        _scopes = scopes;
        _notifier = notifier;
        _console = console;
        _cancellation = cancellation;
        _clock = clock;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var runId in _queue.DequeueAllAsync(stoppingToken).ConfigureAwait(false))
        {
            try
            {
                await ExecuteRunAsync(runId, stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[pipeline-run] Run {Run} crashed in the executor.", runId);
            }
        }
    }

    private async Task ExecuteRunAsync(Guid runId, CancellationToken ct)
    {
        using var scope = _scopes.CreateScope();
        var sp = scope.ServiceProvider;
        var runs = sp.GetRequiredService<IPipelineRunStore>();
        var uow = sp.GetRequiredService<IUnitOfWork>();
        var consoleStore = sp.GetRequiredService<IPipelineRunConsoleStore>();

        var run = await runs.GetByIdAsync(runId, ct).ConfigureAwait(false);
        if (run is null) return;

        // Per-run cancellation: a linked token the cancel endpoint can trip independently of
        // host shutdown. The orchestrator uses it to stop the in-flight Jenkins build.
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct);
        _cancellation.Track(runId, linked);
        try
        {
            var orchestrator = sp.GetService<IPipelineOrchestrator>();
            if (orchestrator is null)
            {
                await SettleAsync(run, uow, consoleStore, () => run.Fail("Jenkins is not configured (set Jenkins:Url + Jenkins:ApiToken).", _clock.GetUtcNow()), runId, ct).ConfigureAwait(false);
                return;
            }

            var pipeline = await sp.GetRequiredService<IPipelineStore>().GetByIdAsync(run.PipelineId, ct).ConfigureAwait(false);
            if (pipeline is null)
            {
                await SettleAsync(run, uow, consoleStore, () => run.Fail("Pipeline definition not found.", _clock.GetUtcNow()), runId, ct).ConfigureAwait(false);
                return;
            }

            var repo = run.RepositoryId is { } rid
                ? await sp.GetRequiredService<ISourceRepositoryStore>().GetByIdAsync(rid, ct).ConfigureAwait(false)
                : null;

            var steps = BuildSteps(pipeline, repo, run.Branch);
            // Report events SYNCHRONOUSLY. A BackgroundService has no SynchronizationContext, so Progress<T> would
            // marshal callbacks to the thread pool — leaving a race where the final console chunk lands AFTER
            // RunAsync returns and SettleAsync has already snapshotted the buffer, dropping it from the persisted
            // copy. Inline dispatch guarantees the buffer is fully populated by the time RunAsync completes.
            var progress = new SyncProgress<PipelineEvent>(evt => OnEvent(runId, evt));

            Jenkins.Orchestrator.PipelineRun result;
            try
            {
                result = await orchestrator.RunAsync(steps, progress, linked.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw; // host shutdown — stop the executor loop (run is left Running)
            }
            catch (OperationCanceledException)
            {
                // Per-run cancel requested via the cancel endpoint.
                await SettleAsync(run, uow, consoleStore, () => run.Cancel(_clock.GetUtcNow()), runId, ct).ConfigureAwait(false);
                return;
            }
            catch (Exception ex)
            {
                await SettleAsync(run, uow, consoleStore, () => run.Fail($"Executor error: {ex.Message}", _clock.GetUtcNow()), runId, ct).ConfigureAwait(false);
                return;
            }

            var now = _clock.GetUtcNow();
            var order = 0;
            foreach (var s in result.Steps)
            {
                order++;
                if (s.Result == BuildResult.Success && s.BuildNumber is int buildNumber)
                    run.RecordStepSucceeded(order, s.JobName, buildNumber, now);
            }

            await SettleAsync(run, uow, consoleStore,
                () => { if (result.Success) run.Succeed(now); else run.Fail(result.FailureReason ?? "Pipeline failed.", now); },
                runId, ct).ConfigureAwait(false);
        }
        finally
        {
            _cancellation.Forget(runId);
        }
    }

    private async Task SettleAsync(Jenkins.Domain.PipelineRuns.PipelineRun run, IUnitOfWork uow,
        IPipelineRunConsoleStore consoleStore, Action settle, Guid runId, CancellationToken ct)
    {
        settle();

        // Snapshot the live console buffer into persisted rows BEFORE it is cleared, so a completed run's logs stay
        // readable via REST. One row per job; build numbers joined from the run's recorded steps (default 0, matching
        // the hub's replay). Tracked on the same scoped DbContext, so it flushes in the settle transaction below.
        var buildByJob = run.Steps
            .GroupBy(s => s.JobName, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.Last().BuildNumber, StringComparer.Ordinal);
        var now = _clock.GetUtcNow();
        var logs = _console.Snapshot(runId)
            .Select(seg => new Jenkins.Domain.PipelineRuns.PipelineRunConsoleLog(
                Guid.NewGuid(), runId, seg.JobName,
                buildByJob.TryGetValue(seg.JobName, out var bn) ? bn : 0,
                seg.Text, now))
            .ToList();
        if (logs.Count > 0) consoleStore.AddRange(logs);

        await uow.SaveChangesAsync(ct).ConfigureAwait(false); // dispatches domain events → translators → integration events
        await _notifier.RunSettledAsync(runId, run.Status.ToString(), run.FailureReason, ct).ConfigureAwait(false); // per-run group (live viewer)
        await _notifier.RunCompletedAsync(runId, run.PipelineName, run.Status.ToString(), run.FailureReason, ct).ConfigureAwait(false); // all clients (app-wide toast)
        _console.Clear(runId);
    }

    /// <summary>Reports progress on the caller's thread (unlike <see cref="Progress{T}"/>, which hops to the thread
    /// pool when there's no <see cref="SynchronizationContext"/>) so console events are buffered before RunAsync returns.</summary>
    private sealed class SyncProgress<T> : IProgress<T>
    {
        private readonly Action<T> _handler;
        public SyncProgress(Action<T> handler) => _handler = handler;
        public void Report(T value) => _handler(value);
    }

    private void OnEvent(Guid runId, PipelineEvent evt)
    {
        switch (evt)
        {
            case PipelineStepLogChunk log:
                _console.Append(runId, log.JobName, log.Text);
                _ = _notifier.ConsoleAppendedAsync(runId, log.JobName, log.BuildNumber, log.Text);
                break;
            case PipelineStepStarted s:
                _ = _notifier.StepChangedAsync(runId, new PipelineRunStepUpdate(s.JobName, "Started", null, null));
                break;
            case PipelineStepQueued q:
                _ = _notifier.StepChangedAsync(runId, new PipelineRunStepUpdate(q.JobName, "Queued", null, null));
                break;
            case PipelineStepRunning r:
                _ = _notifier.StepChangedAsync(runId, new PipelineRunStepUpdate(r.JobName, "Running", r.BuildNumber, null));
                break;
            case PipelineStepCompleted c:
                _ = _notifier.StepChangedAsync(runId, new PipelineRunStepUpdate(c.JobName, c.Result.ToString(), c.BuildNumber, null));
                break;
            case PipelineStepFailed f:
                _ = _notifier.StepChangedAsync(runId, new PipelineRunStepUpdate(f.JobName, "Failed", null, f.Reason));
                break;
        }
    }

    /// <summary>The Jenkins job that builds + pushes the container image (the per-repo gate target).</summary>
    private const string ContainerPublishJobName = "cicd-publish-nexus-docker";

    private static IReadOnlyList<PipelineStep> BuildSteps(Pipeline pipeline, SourceRepository? repo, string? branch)
    {
        var steps = new List<PipelineStep>();
        foreach (var stage in pipeline.Stages)
        {
            // Per-repo gate (combines with the code-level Containerizable opt-in): if the repo
            // denies container production, drop the container-publish stage. NuGet + build stages
            // are unaffected. (Library-only builds also self-skip in the job via an empty manifest.)
            if (repo is { AllowContainerPublish: false } &&
                string.Equals(stage.JobName, ContainerPublishJobName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var upstreamless = string.IsNullOrWhiteSpace(stage.UpstreamJobName);
            var pars = new Dictionary<string, string>(StringComparer.Ordinal);
            if (repo is not null && upstreamless)
            {
                // Source steps clone the repo; downstream steps consume artifacts (decision
                // mirrored from the orchestrator UI's EffectiveSteps).
                pars["GIT_URL"] = repo.GitUrl;
                pars["GIT_BRANCH"] = string.IsNullOrWhiteSpace(branch) ? repo.DefaultBranch : branch!;
                if (!string.IsNullOrWhiteSpace(repo.BaseVersion))
                    pars["BASE_VER"] = repo.BaseVersion; // versioning base for the build job
                if (!string.IsNullOrWhiteSpace(repo.AppHostPath))
                    pars["APPHOST_PROJECT"] = repo.AppHostPath!; // Aspire build: where the AppHost lives
            }
            foreach (var kv in stage.Parameters) pars[kv.Key] = kv.Value; // explicit stage params win
            steps.Add(new PipelineStep(
                stage.JobName,
                upstreamless ? null : stage.UpstreamJobName,
                pars.Count > 0 ? pars : null));
        }
        return steps;
    }
}
