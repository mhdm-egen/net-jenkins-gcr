using FluentValidation;
using Publisher.Application.Features.Channels;
using Publisher.Contracts.Channels;

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

        // Promotion to the remote registry (Nexus → GAR) is the next slice; not implemented yet.
        group.MapPost("{name}/publish", (string name) =>
            Results.Problem(
                title: "Not implemented",
                detail: $"Promoting channel '{name}' to a remote registry (Nexus → Google Artifact Registry) is not yet implemented.",
                statusCode: StatusCodes.Status501NotImplemented))
            .WithTags("Channels");

        return app;
    }
}
