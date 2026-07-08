using Microsoft.Extensions.Logging;
using Deployment.Domain.AspireApps;
using Deployment.Domain.Previews;

namespace Deployment.Application.Features.Previews;

public sealed record PreviewWebhookResult(bool Applied, string Outcome);

public sealed record PreviewWebhookCommand(string? AppName, Guid? ApplicationId, string Key, string? Action);

/// <summary>
/// Handles a normalized preview-lifecycle webhook (git provider / Jenkins → deployment). On a close/merge/delete
/// action it tears down the preview matching the app + key (branch / PR), reusing
/// <see cref="TeardownPreviewEnvironmentHandler"/>. Non-teardown actions are acknowledged as no-ops. The TTL
/// sweeper remains the safety net if the webhook never fires.
/// </summary>
public sealed class HandlePreviewWebhookHandler
{
    private static readonly HashSet<string> TeardownActions =
        new(StringComparer.OrdinalIgnoreCase) { "closed", "close", "merged", "merge", "deleted", "delete" };

    private readonly IAspireApplicationRepository _apps;
    private readonly IPreviewEnvironmentRepository _previews;
    private readonly TeardownPreviewEnvironmentHandler _teardown;
    private readonly ILogger<HandlePreviewWebhookHandler> _logger;

    public HandlePreviewWebhookHandler(
        IAspireApplicationRepository apps, IPreviewEnvironmentRepository previews,
        TeardownPreviewEnvironmentHandler teardown, ILogger<HandlePreviewWebhookHandler> logger)
    {
        _apps = apps; _previews = previews; _teardown = teardown; _logger = logger;
    }

    public async Task<PreviewWebhookResult> HandleAsync(PreviewWebhookCommand cmd, CancellationToken ct = default)
    {
        if (!string.IsNullOrWhiteSpace(cmd.Action) && !TeardownActions.Contains(cmd.Action.Trim()))
            return new PreviewWebhookResult(false, $"ignored-action:{cmd.Action.Trim()}");

        if (string.IsNullOrWhiteSpace(cmd.Key)) return new PreviewWebhookResult(false, "key-required");

        Guid appId;
        if (cmd.ApplicationId is { } id && id != Guid.Empty)
        {
            appId = id;
        }
        else if (!string.IsNullOrWhiteSpace(cmd.AppName))
        {
            var app = await _apps.FindBySourceKeyAsync(cmd.AppName, ct).ConfigureAwait(false);
            if (app is null) return new PreviewWebhookResult(false, "app-not-found");
            appId = app.Id;
        }
        else
        {
            return new PreviewWebhookResult(false, "app-not-identified");
        }

        var key = PreviewNaming.SlugKey(cmd.Key);
        var preview = await _previews.FindLiveByAppAndKeyAsync(appId, key, ct).ConfigureAwait(false);
        if (preview is null) return new PreviewWebhookResult(false, "no-live-preview");

        var result = await _teardown.HandleAsync(new TeardownPreviewEnvironmentCommand(preview.Id, "webhook"), ct).ConfigureAwait(false);
        _logger.LogInformation("[preview] webhook teardown app {App} key '{Key}' -> {Outcome}.", appId, key, result.Outcome);
        return new PreviewWebhookResult(result.Applied, result.Outcome);
    }
}
