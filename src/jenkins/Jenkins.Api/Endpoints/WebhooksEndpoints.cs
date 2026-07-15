using Jenkins.Application.Features.Webhooks;
using Jenkins.Contracts.Webhooks;

namespace Jenkins.Api.Endpoints;

/// <summary>
/// Inbound git PR-lifecycle webhook. A provider adapter (or a Jenkins post-build step) posts the
/// normalized shape here; opened/synchronize builds the PR branch (→ preview), closed/merged tears the
/// preview down. Always HTTP 200 — the body reports the outcome (webhooks expect a 200 ack).
/// </summary>
public static class WebhooksEndpoints
{
    public static IEndpointRouteBuilder MapWebhookEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/jenkins/webhooks/git", async (
            GitWebhookRequest body,
            HandleGitWebhookHandler handler,
            CancellationToken ct) =>
        {
            var result = await handler.HandleAsync(
                new GitWebhookCommand(body.Repository, body.Branch, body.PrNumber, body.Action, body.AppName), ct);
            return Results.Ok(result);
        }).WithTags("Webhooks");

        return app;
    }
}
