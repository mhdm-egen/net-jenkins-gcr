using Microsoft.Extensions.Logging;
using Jenkins.Application.Abstractions;
using Jenkins.Application.Features.PipelineRuns;
using Jenkins.Domain.Pipelines;
using Jenkins.Domain.SourceRepositories;

namespace Jenkins.Application.Features.Webhooks;

public sealed record GitWebhookResult(string Outcome, Guid? RunId = null);

public sealed record GitWebhookCommand(string? Repository, string? Branch, int? PrNumber, string? Action, string? AppName);

/// <summary>
/// Normalized git PR-lifecycle ingress. Resolves the registered <see cref="SourceRepository"/> by name,
/// then routes by action: open/synchronize/reopen → start the repo's Aspire pipeline on the PR branch
/// (which flows through ingestion → <c>AspireAppPublished(Branch)</c> → a preview environment); close/
/// merge/delete → ask the deployment service to tear the matching preview down (reusing its existing
/// <c>/previews/webhook</c>). Previews are Aspire-only; non-Aspire repos are acknowledged as no-ops.
/// </summary>
public sealed class HandleGitWebhookHandler
{
    private const string AspirePipelineName = "Aspire build";

    private static readonly HashSet<string> OpenActions =
        new(StringComparer.OrdinalIgnoreCase) { "opened", "synchronize", "synchronized", "reopened", "updated" };
    private static readonly HashSet<string> CloseActions =
        new(StringComparer.OrdinalIgnoreCase) { "closed", "close", "merged", "merge", "deleted", "delete" };

    private readonly ISourceRepositoryStore _repos;
    private readonly IPipelineStore _pipelines;
    private readonly StartPipelineRunHandler _startRun;
    private readonly IDeploymentPreviewClient _previews;
    private readonly ILogger<HandleGitWebhookHandler> _logger;

    public HandleGitWebhookHandler(
        ISourceRepositoryStore repos, IPipelineStore pipelines, StartPipelineRunHandler startRun,
        IDeploymentPreviewClient previews, ILogger<HandleGitWebhookHandler> logger)
    {
        _repos = repos; _pipelines = pipelines; _startRun = startRun; _previews = previews; _logger = logger;
    }

    public async Task<GitWebhookResult> HandleAsync(GitWebhookCommand cmd, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(cmd.Branch)) return new GitWebhookResult("branch-required");
        if (string.IsNullOrWhiteSpace(cmd.Action)) return new GitWebhookResult("action-required");

        var action = cmd.Action.Trim();
        var isOpen = OpenActions.Contains(action);
        var isClose = CloseActions.Contains(action);
        if (!isOpen && !isClose) return new GitWebhookResult($"ignored-action:{action}");

        var repo = string.IsNullOrWhiteSpace(cmd.Repository)
            ? null
            : await _repos.FindByNameAsync(cmd.Repository.Trim(), ct).ConfigureAwait(false);
        if (repo is null) return new GitWebhookResult("repo-not-found");
        if (repo.BuildKind != BuildKind.Aspire) return new GitWebhookResult("not-an-aspire-repo");

        var branch = cmd.Branch.Trim();

        if (isClose)
        {
            var appName = string.IsNullOrWhiteSpace(cmd.AppName) ? repo.Name : cmd.AppName!.Trim();
            await _previews.TeardownPreviewAsync(appName, branch, action, ct).ConfigureAwait(false);
            _logger.LogInformation("[webhook] PR {Action} on {Repo}/{Branch} -> preview teardown requested (app {App}).",
                action, repo.Name, branch, appName);
            return new GitWebhookResult("teardown-requested");
        }

        // open/synchronize → build the PR branch. The default branch is a normal deploy, not a preview.
        if (string.Equals(branch, repo.DefaultBranch, StringComparison.OrdinalIgnoreCase))
            return new GitWebhookResult("default-branch-skipped");

        var pipeline = await _pipelines.FindByNameAsync(AspirePipelineName, ct).ConfigureAwait(false);
        if (pipeline is null) return new GitWebhookResult("aspire-pipeline-not-found");

        var triggeredBy = cmd.PrNumber is { } pr ? $"git:pr-{pr}" : $"git:{branch}";
        var runId = await _startRun.HandleAsync(
            new StartPipelineRunCommand(pipeline.Id, repo.Id, triggeredBy, branch), ct).ConfigureAwait(false);
        _logger.LogInformation("[webhook] PR {Action} on {Repo}/{Branch} -> Aspire run {Run}.", action, repo.Name, branch, runId);
        return new GitWebhookResult("build-triggered", runId);
    }
}
