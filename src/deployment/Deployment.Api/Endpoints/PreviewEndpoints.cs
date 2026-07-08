using Deployment.Application.Features.Previews;
using Deployment.Contracts.Previews;

namespace Deployment.Api.Endpoints;

public static class PreviewEndpoints
{
    public static IEndpointRouteBuilder MapPreviewEndpoints(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/deployment/previews").WithTags("Previews");

        g.MapGet("", async (Guid? applicationId, bool? includeTornDown, ListPreviewEnvironmentsHandler h, CancellationToken ct) =>
            Results.Ok(await h.HandleAsync(new ListPreviewEnvironmentsQuery(applicationId, includeTornDown ?? false), ct)));

        g.MapGet("{id:guid}", async (Guid id, GetPreviewEnvironmentByIdHandler h, CancellationToken ct) =>
            await h.HandleAsync(new GetPreviewEnvironmentByIdQuery(id), ct) is { } d ? Results.Ok(d) : Results.NotFound());

        g.MapPost("", async (CreatePreviewEnvironmentRequest body, CreatePreviewEnvironmentHandler h, CancellationToken ct) =>
        {
            try
            {
                var result = await h.HandleAsync(
                    new CreatePreviewEnvironmentCommand(body.ApplicationId, body.Key, body.ManifestSource, body.Version, body.TtlHours, body.TriggeredBy), ct);
                return Results.Accepted($"/api/deployment/previews/{result.PreviewId}", result);
            }
            catch (InvalidOperationException ex) { return Results.Problem(title: "Cannot create preview", detail: ex.Message, statusCode: 400); }
        });

        g.MapPost("{id:guid}/teardown", async (Guid id, TeardownPreviewEnvironmentHandler h, CancellationToken ct) =>
        {
            var r = await h.HandleAsync(new TeardownPreviewEnvironmentCommand(id, "ui"), ct);
            return r.Applied ? Results.Ok(r) : Results.Problem(title: "Cannot tear down", detail: r.Outcome, statusCode: 404);
        });

        // PR-lifecycle webhook: a git provider / Jenkins posts here on PR close/merge to tear down the matching
        // preview. Always 200 (webhooks expect it); the result body says what happened. The TTL sweeper is the
        // fallback if this never fires.
        g.MapPost("webhook", async (PreviewWebhookRequest body, HandlePreviewWebhookHandler h, CancellationToken ct) =>
        {
            var r = await h.HandleAsync(new PreviewWebhookCommand(body.AppName, body.ApplicationId, body.Key, body.Action), ct);
            return Results.Ok(r);
        });

        return app;
    }
}
