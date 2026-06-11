using Publisher.Application.Features.Containers;

namespace Publisher.Api.Endpoints;

/// <summary>
/// Read surface over the publisher's container inventory (sourced from local Nexus via the
/// CI <c>ContainerPublished</c> bus event). Write-side ingestion happens on the bus, not HTTP.
/// </summary>
public static class ContainersEndpoints
{
    public static IEndpointRouteBuilder MapContainersEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/publisher/containers").WithTags("Containers");

        group.MapGet("", async (
            Guid? repositoryId,
            string? containerName,
            ListContainersHandler handler,
            CancellationToken ct) =>
        {
            var list = await handler.HandleAsync(new ListContainersQuery(repositoryId, containerName), ct);
            return Results.Ok(list);
        });

        group.MapGet("{id:guid}", async (
            Guid id,
            GetContainerByIdHandler handler,
            CancellationToken ct) =>
        {
            var hit = await handler.HandleAsync(new GetContainerByIdQuery(id), ct);
            return hit is null ? Results.NotFound() : Results.Ok(hit);
        });

        return app;
    }
}
