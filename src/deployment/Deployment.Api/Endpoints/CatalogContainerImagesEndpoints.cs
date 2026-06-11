using Deployment.Application.Features.Catalog.ContainerImages;
using Deployment.Contracts.Catalog;
using FluentValidation;

namespace Deployment.Api.Endpoints;

/// <summary>
/// HTTP surface for the <c>ContainerImage</c> catalog (the container coordinate a Service
/// is backed by). CRUD + activate/deactivate, plus live registry queries (list tags /
/// resolve a tag to a digest) used by the release modal. See
/// <c>docs/deployment/container-image-source.md</c>.
/// </summary>
public static class CatalogContainerImagesEndpoints
{
    public static IEndpointRouteBuilder MapCatalogContainerImageEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/deployment/container-images").WithTags("Catalog: Container Images");

        // --- Reads ---

        group.MapGet("", async (
            bool? onlyActive,
            ListContainerImagesHandler handler,
            CancellationToken ct) =>
        {
            return Results.Ok(await handler.HandleAsync(new ListContainerImagesQuery(onlyActive), ct));
        });

        group.MapGet("{id:guid}", async (
            Guid id,
            GetContainerImageByIdHandler handler,
            CancellationToken ct) =>
        {
            var hit = await handler.HandleAsync(new GetContainerImageByIdQuery(id), ct);
            return hit is null ? Results.NotFound() : Results.Ok(hit);
        });

        // --- Live registry discovery (release modal) ---

        group.MapGet("tags", async (
            string registry,
            string repository,
            string name,
            ListContainerImageTagsHandler handler,
            CancellationToken ct) =>
        {
            return Results.Ok(await handler.HandleAsync(new ListContainerImageTagsQuery(registry, repository, name), ct));
        });

        group.MapGet("resolve", async (
            string registry,
            string repository,
            string name,
            string tag,
            ResolveContainerImageHandler handler,
            CancellationToken ct) =>
        {
            var hit = await handler.HandleAsync(new ResolveContainerImageQuery(registry, repository, name, tag), ct);
            return hit is null ? Results.NotFound() : Results.Ok(hit);
        });

        // --- Lifecycle ---

        group.MapPost("", async (
            RegisterContainerImageRequest body,
            RegisterContainerImageHandler handler,
            IValidator<RegisterContainerImageCommand> validator,
            CancellationToken ct) =>
        {
            var cmd = new RegisterContainerImageCommand(
                Guid.NewGuid(), body.Registry, body.Repository, body.Name, body.DefaultTag);
            return await CatalogServicesEndpoints.ValidateAndRun(validator, cmd, ct, async () =>
            {
                var dto = await handler.HandleAsync(cmd, ct);
                return Results.Created($"/api/deployment/container-images/{dto.Id}", dto);
            });
        });

        group.MapPost("{id:guid}/default-tag", async (
            Guid id,
            ChangeContainerImageDefaultTagRequest body,
            ChangeContainerImageDefaultTagHandler handler,
            IValidator<ChangeContainerImageDefaultTagCommand> validator,
            CancellationToken ct) =>
        {
            var cmd = new ChangeContainerImageDefaultTagCommand(id, body.DefaultTag);
            return await CatalogServicesEndpoints.ValidateAndRun(validator, cmd, ct, async () =>
                Results.Ok(await handler.HandleAsync(cmd, ct)));
        });

        group.MapPost("{id:guid}/active", async (
            Guid id,
            SetContainerImageActiveRequest body,
            ChangeContainerImageActivationHandler handler,
            IValidator<ChangeContainerImageActivationCommand> validator,
            CancellationToken ct) =>
        {
            var cmd = new ChangeContainerImageActivationCommand(id, body.IsActive);
            return await CatalogServicesEndpoints.ValidateAndRun(validator, cmd, ct, async () =>
                Results.Ok(await handler.HandleAsync(cmd, ct)));
        });

        return app;
    }
}
