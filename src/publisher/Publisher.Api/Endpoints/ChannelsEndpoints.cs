using FluentValidation;
using Publisher.Application.Features.Channels;
using Publisher.Application.Features.Promotions;
using Publisher.Contracts.Channels;
using Publisher.Contracts.Promotions;

namespace Publisher.Api.Endpoints;

/// <summary>
/// HTTP surface for publishable names (channels). A channel is a mutable alias that points at one
/// inventory container at a time; tagging a container publishable creates or moves the alias.
/// The actual promotion to a remote registry (Nexus → GAR) is stubbed for now.
/// </summary>
public static class ChannelsEndpoints
{
    public static IEndpointRouteBuilder MapChannelsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/publisher/channels").WithTags("Channels");

        group.MapGet("", async (ListChannelsHandler handler, CancellationToken ct) =>
            Results.Ok(await handler.HandleAsync(new ListChannelsQuery(), ct)));

        group.MapGet("{name}", async (string name, GetChannelByNameHandler handler, CancellationToken ct) =>
        {
            var hit = await handler.HandleAsync(new GetChannelByNameQuery(name), ct);
            return hit is null ? Results.NotFound() : Results.Ok(hit);
        });

        // Tag a container as publishable under {name} — create the channel or move its pointer.
        group.MapPut("{name}", async (
            string name,
            TagPublishableRequest body,
            TagContainerPublishableHandler handler,
            IValidator<TagContainerPublishableCommand> validator,
            CancellationToken ct) =>
        {
            var cmd = new TagContainerPublishableCommand(name, body.ContainerId, body.BoundBy);
            return await EndpointHelpers.ValidateAndRun(validator, cmd, ct, async () =>
            {
                await handler.HandleAsync(cmd, ct);
                return Results.Ok(new { channel = name, containerId = body.ContainerId });
            });
        });

        // Publish the channel's current container to a remote registry (the one specified, or the
        // default). Convenience wrapper over the container-promote path.
        group.MapPost("{name}/publish", async (
            string name,
            PromoteContainerRequest body,
            GetChannelByNameHandler channels,
            RequestManualPromotionHandler promote,
            CancellationToken ct) =>
        {
            var channel = await channels.HandleAsync(new GetChannelByNameQuery(name), ct);
            if (channel is null) return Results.NotFound();

            try
            {
                var result = await promote.HandleAsync(
                    new RequestManualPromotionCommand(channel.CurrentContainerId, body.RegistryId, body.TriggeredBy ?? $"channel:{name}"), ct);
                return result.PromotionId is null
                    ? Results.Ok(new { outcome = result.Outcome })
                    : Results.Accepted($"/api/publisher/promotions/{result.PromotionId}",
                        new { promotionId = result.PromotionId, outcome = result.Outcome });
            }
            catch (InvalidOperationException ex)
            {
                return Results.Problem(title: "Cannot publish channel", detail: ex.Message, statusCode: 409);
            }
        });

        return app;
    }
}
