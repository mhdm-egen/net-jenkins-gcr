using Publisher.Application.Features.Promotions;
using Publisher.Contracts.Promotions;

namespace Publisher.Api.Endpoints;

/// <summary>
/// Promotion history (read) plus the manual trigger to push a container to a remote registry.
/// The manual push runs the same path the rules use (idempotent; settled asynchronously).
/// </summary>
public static class PromotionsEndpoints
{
    public static IEndpointRouteBuilder MapPromotionsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/publisher/promotions").WithTags("Promotions");

        group.MapGet("", async (
            Guid? containerId,
            Guid? registryId,
            ListPromotionsHandler handler,
            CancellationToken ct) =>
            Results.Ok(await handler.HandleAsync(new ListPromotionsQuery(containerId, registryId), ct)));

        group.MapGet("{id:guid}", async (Guid id, GetPromotionByIdHandler handler, CancellationToken ct) =>
        {
            var hit = await handler.HandleAsync(new GetPromotionByIdQuery(id), ct);
            return hit is null ? Results.NotFound() : Results.Ok(hit);
        });

        // Manual promotion of an inventory container (to a named registry, or the default).
        app.MapPost("/api/publisher/containers/{containerId:guid}/promote", async (
            Guid containerId,
            PromoteContainerRequest body,
            RequestManualPromotionHandler handler,
            CancellationToken ct) =>
        {
            try
            {
                var result = await handler.HandleAsync(
                    new RequestManualPromotionCommand(containerId, body.RegistryId, body.TriggeredBy), ct);
                return result.PromotionId is null
                    ? Results.Ok(new { outcome = result.Outcome })
                    : Results.Accepted($"/api/publisher/promotions/{result.PromotionId}",
                        new { promotionId = result.PromotionId, outcome = result.Outcome });
            }
            catch (InvalidOperationException ex)
            {
                return Results.Problem(title: "Cannot promote", detail: ex.Message, statusCode: 409);
            }
        }).WithTags("Promotions");

        return app;
    }
}
