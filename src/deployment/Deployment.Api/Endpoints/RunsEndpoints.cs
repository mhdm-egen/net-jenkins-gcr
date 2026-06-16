using Deployment.Application.Features.Containers;
using Deployment.Application.Features.Runs;

namespace Deployment.Api.Endpoints;

public static class RunsEndpoints
{
    public static IEndpointRouteBuilder MapRunEndpoints(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/deployment/runs").WithTags("Runs");
        g.MapGet("", async (Guid? serviceId, Guid? mappingId, ListRunsHandler h, CancellationToken ct) =>
            Results.Ok(await h.HandleAsync(new ListRunsQuery(serviceId, mappingId), ct)));
        g.MapGet("{id:guid}", async (Guid id, GetRunByIdHandler h, CancellationToken ct) =>
            await h.HandleAsync(new GetRunByIdQuery(id), ct) is { } d ? Results.Ok(d) : Results.NotFound());

        // The light container inventory (latest push per name) — what manual deploys draw from.
        app.MapGet("/api/deployment/containers", async (ListKnownContainersHandler h, CancellationToken ct) =>
            Results.Ok(await h.HandleAsync(new ListKnownContainersQuery(), ct))).WithTags("Containers");

        return app;
    }
}
